using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Justloccit.Function.Models;
using System.Linq;
using System.Collections.Generic;

namespace Justloccit.Function.Services
{
    public class ReservationService
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Container _reservationsContainer;
        private readonly Container _subServicesContainer;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        
        // Configuration keys - these match the keys in the deployment scripts
        private const string ReservationsDatabaseConfigKey = "CosmosDb:JustloccitReservations";
        private const string ReservationsContainerConfigKey = "CosmosDb:ReservationsContainer";
        private const string ServicesDatabaseConfigKey = "CosmosDb:ServicesDatabase";
        private const string SubServicesContainerConfigKey = "CosmosDb:SubServicesContainer";
        
        public ReservationService(CosmosClient cosmosClient, IConfiguration configuration, ILogger<ReservationService> logger)
        {
            _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            string reservationsDatabase = GetRequiredConfigValue(ReservationsDatabaseConfigKey);
            string reservationsContainer = GetRequiredConfigValue(ReservationsContainerConfigKey);
            string servicesDatabase = GetRequiredConfigValue(ServicesDatabaseConfigKey);
            string subServicesContainer = GetRequiredConfigValue(SubServicesContainerConfigKey);
            
            try
            {
                _reservationsContainer = _cosmosClient.GetContainer(reservationsDatabase, reservationsContainer);
                _subServicesContainer = _cosmosClient.GetContainer(servicesDatabase, subServicesContainer);
                
                _logger.LogInformation("Successfully initialized Cosmos DB containers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Cosmos DB containers");
                throw;
            }
        }
        
        private string GetRequiredConfigValue(string key)
        {
            string value = _configuration[key];
            if (string.IsNullOrEmpty(value))
            {
                _logger.LogError("Required configuration value for '{Key}' is missing", key);
                throw new InvalidOperationException($"Required configuration value for '{key}' is missing");
            }
            return value;
        }
        
        public async Task<(bool success, string serviceName, int duration)> GetSubServiceDetailsAsync(string subServiceId)
        {
            if (string.IsNullOrEmpty(subServiceId))
            {
                _logger.LogWarning("GetSubServiceDetailsAsync called with null or empty subServiceId");
                return (false, string.Empty, 0);
            }
            
            try
            {
                // Query to get sub-service details
                var query = new QueryDefinition(
                    "SELECT c.name, c.duration FROM c WHERE c.id = @id")
                    .WithParameter("@id", subServiceId);
                    
                var iterator = _subServicesContainer.GetItemQueryIterator<dynamic>(query);
                
                if (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    var subService = response.FirstOrDefault();
                    
                    if (subService != null)
                    {
                        return (true, subService.name.ToString(), (int)subService.duration);
                    }
                }
                
                _logger.LogWarning("Sub-service with ID {SubServiceId} was not found", subServiceId);
                return (false, string.Empty, 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sub-service details for ID {SubServiceId}", subServiceId);
                return (false, string.Empty, 0);
            }
        }
        
        public async Task<List<(string id, string name, int duration)>> GetMultipleSubServiceDetailsAsync(List<SubServiceRequest> subServices)
        {
            if (subServices == null || !subServices.Any())
            {
                _logger.LogWarning("GetMultipleSubServiceDetailsAsync called with null or empty subServices list");
                return new List<(string, string, int)>();
            }
            
            var result = new List<(string id, string name, int duration)>();
            
            foreach (var subService in subServices)
            {
                if (string.IsNullOrEmpty(subService.SubServiceId))
                {
                    _logger.LogWarning("Skipping item with null or empty SubServiceId");
                    continue;
                }
                
                try
                {
                    // Query to get sub-service details by ID
                    var query = new QueryDefinition(
                        "SELECT c.id, c.name FROM c WHERE c.id = @id")
                        .WithParameter("@id", subService.SubServiceId);
                        
                    var iterator = _subServicesContainer.GetItemQueryIterator<dynamic>(query);
                    
                    if (iterator.HasMoreResults)
                    {
                        var response = await iterator.ReadNextAsync();
                        var service = response.FirstOrDefault();
                        
                        if (service != null)
                        {
                            result.Add((
                                subService.SubServiceId,
                                service.name.ToString(),
                                subService.Duration
                            ));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving sub-service details for ID {SubServiceId}", subService.SubServiceId);
                }
            }
            
            return result;
        }
        
        public async Task<(Reservation reservation, bool success)> CreateSingleServiceReservationAsync(string subServiceId, string date, string startTime, string serviceName, int duration)
        {
            if (string.IsNullOrEmpty(subServiceId) || string.IsNullOrEmpty(date) || string.IsNullOrEmpty(startTime))
            {
                _logger.LogWarning("CreateSingleServiceReservationAsync called with invalid parameters");
                return (null, false);
            }
            
            try
            {
                // Calculate end time
                var startTimeObj = TimeSpan.Parse(startTime);
                var endTimeObj = startTimeObj.Add(TimeSpan.FromMinutes(duration));
                var endTime = $"{endTimeObj.Hours:D2}:{endTimeObj.Minutes:D2}";
                
                // Create the reservation
                var reservation = new Reservation
                {
                    SubServiceId = subServiceId,
                    Date = date,
                    StartTime = startTime,
                    EndTime = endTime,
                    Duration = duration,
                    Status = ReservationStatus.Locked,
                    LockExpiresAt = DateTime.UtcNow.AddMinutes(8), // 8 minutes lock time
                    PartitionKey = date,
                    ServiceName = serviceName
                };
                
                // Save the reservation to CosmosDB
                var response = await _reservationsContainer.CreateItemAsync(
                    reservation,
                    new PartitionKey(reservation.PartitionKey)
                );
                
                if (response.StatusCode == HttpStatusCode.Created)
                {
                    return (reservation, true);
                }
                
                _logger.LogWarning("Failed to create reservation. Status code: {StatusCode}", response.StatusCode);
                return (null, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating single service reservation");
                return (null, false);
            }
        }
        
        public async Task<(Reservation reservation, bool success)> CreateMultiServiceReservationAsync(string date, string startTime, List<(string id, string name, int duration)> subServices)
        {
            if (string.IsNullOrEmpty(date) || string.IsNullOrEmpty(startTime) || subServices == null || !subServices.Any())
            {
                _logger.LogWarning("CreateMultiServiceReservationAsync called with invalid parameters");
                return (null, false);
            }
            
            try
            {
                // Calculate total duration and end time
                int totalDuration = subServices.Sum(s => s.duration);
                var startTimeObj = TimeSpan.Parse(startTime);
                var endTimeObj = startTimeObj.Add(TimeSpan.FromMinutes(totalDuration));
                var endTime = $"{endTimeObj.Hours:D2}:{endTimeObj.Minutes:D2}";
                
                // Create the reservation with multiple sub-services
                var reservation = new Reservation
                {
                    Date = date,
                    StartTime = startTime,
                    EndTime = endTime,
                    Duration = totalDuration,
                    Status = ReservationStatus.Locked,
                    LockExpiresAt = DateTime.UtcNow.AddMinutes(8), // 8 minutes lock time
                    PartitionKey = date,
                    SubServices = subServices.Select(s => new SubServiceReservation
                    {
                        Id = s.id,
                        SubServiceId = s.id,
                        Name = s.name,
                        Duration = s.duration
                    }).ToList()
                };
                
                // Save the reservation to CosmosDB
                var response = await _reservationsContainer.CreateItemAsync(
                    reservation,
                    new PartitionKey(reservation.PartitionKey)
                );
                
                if (response.StatusCode == HttpStatusCode.Created)
                {
                    return (reservation, true);
                }
                
                _logger.LogWarning("Failed to create multi-service reservation. Status code: {StatusCode}", response.StatusCode);
                return (null, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating multi-service reservation");
                return (null, false);
            }
        }
        
        public async Task<bool> ReleaseReservationAsync(string bookingId, string date)
        {
            if (string.IsNullOrEmpty(bookingId) || string.IsNullOrEmpty(date))
            {
                _logger.LogWarning("ReleaseReservationAsync called with invalid parameters");
                return false;
            }
            
            try
            {
                // Find the reservation by ID
                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.id = @id AND c.date = @date")
                    .WithParameter("@id", bookingId)
                    .WithParameter("@date", date);
                    
                var iterator = _reservationsContainer.GetItemQueryIterator<Reservation>(query);
                
                if (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    var reservation = response.FirstOrDefault();
                    
                    if (reservation != null)
                    {
                        // Update bookingStatus  to cancelled
                        reservation.Status = ReservationStatus.Cancelled;
                        
                        // Update the item in CosmosDB
                        await _reservationsContainer.ReplaceItemAsync(
                            reservation,
                            reservation.Id,
                            new PartitionKey(reservation.PartitionKey)
                        );
                        
                        return true;
                    }
                }
                
                _logger.LogWarning("Reservation with ID {BookingId} for date {Date} not found", bookingId, date);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing reservation {BookingId}", bookingId);
                return false;
            }
        }
    }
} 