using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Justloccit.Function.Models;
using Justloccit.Function.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Justloccit.Function
{
    public class LockReservation
    {
        private readonly ILogger<LockReservation> _logger;
        private readonly ReservationService _reservationService;

        public LockReservation(ILogger<LockReservation> logger, ReservationService reservationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _reservationService = reservationService ?? throw new ArgumentNullException(nameof(reservationService));
        }

        [Function("LockSubServiceAsync")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "LockSubServiceAsync")] HttpRequest req)
        {
            _logger.LogInformation("Processing lock reservation request");

            if (req == null)
            {
                _logger.LogError("Request object is null");
                return new BadRequestObjectResult("Invalid request");
            }

            try
            {
                // Read request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                
                if (string.IsNullOrEmpty(requestBody))
                {
                    _logger.LogWarning("Empty request body received");
                    return new BadRequestObjectResult("Request body cannot be empty");
                }
                
                _logger.LogDebug("Parsing request body");
                
                // First try to parse as a single service request
                LockSingleServiceRequest singleRequest = null;
                LockMultipleServicesRequest multiRequest = null;
                
                try
                {
                    singleRequest = JsonSerializer.Deserialize<LockSingleServiceRequest>(requestBody, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    // Simple validation check - if it's a multi-service request, the SubServiceId will be null
                    if (string.IsNullOrEmpty(singleRequest?.SubServiceId))
                    {
                        _logger.LogDebug("Not a valid single service request, SubServiceId is missing");
                        singleRequest = null;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogDebug(ex, "Could not parse as single service request, trying multi-service format");
                    singleRequest = null;
                }
                
                // If not a single service request, try to parse as a multi-service request
                if (singleRequest == null)
                {
                    try
                    {
                        multiRequest = JsonSerializer.Deserialize<LockMultipleServicesRequest>(requestBody, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        
                        // Simple validation check
                        if (multiRequest?.SubServices == null || multiRequest.SubServices.Count == 0)
                        {
                            _logger.LogWarning("Invalid multi-service request: missing or empty subServices array");
                            return new BadRequestObjectResult("Invalid request. Missing subServices array or it's empty.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing request body");
                        return new BadRequestObjectResult("Invalid request format");
                    }
                }
                
                // Process single service request
                if (singleRequest != null)
                {
                    _logger.LogInformation("Processing single service reservation request for date {Date} at {StartTime}", 
                        singleRequest.Date, singleRequest.StartTime);
                    
                    // Get sub-service details from Cosmos DB
                    var (success, serviceName, duration) = 
                        await _reservationService.GetSubServiceDetailsAsync(singleRequest.SubServiceId);
                    
                    if (!success)
                    {
                        _logger.LogWarning("Sub-service with ID {SubServiceId} not found", singleRequest.SubServiceId);
                        return new NotFoundObjectResult($"Sub-service with ID {singleRequest.SubServiceId} not found");
                    }
                    
                    // Create the reservation
                    var (reservation, createSuccess) = 
                        await _reservationService.CreateSingleServiceReservationAsync(
                            singleRequest.SubServiceId, 
                            singleRequest.Date, 
                            singleRequest.StartTime,
                            serviceName,
                            duration);
                    
                    if (!createSuccess || reservation == null)
                    {
                        _logger.LogError("Failed to create single service reservation");
                        return new ObjectResult("Failed to create reservation") { StatusCode = (int)HttpStatusCode.InternalServerError };
                    }
                    
                    // Return response
                    var response = new LockSingleServiceResponse
                    {
                        Success = true,
                        BookingId = reservation.Id,
                        LockExpiresAt = reservation.LockExpiresAt,
                        StartTime = reservation.StartTime,
                        EndTime = reservation.EndTime,
                        Duration = reservation.Duration,
                        ServiceName = serviceName
                    };
                    
                    _logger.LogInformation("Successfully created single service reservation with ID {ReservationId}", reservation.Id);
                    return new OkObjectResult(response);
                }
                else if (multiRequest != null)
                {
                    _logger.LogInformation("Processing multi-service reservation request for date {Date} at {StartTime} with {ServiceCount} services", 
                        multiRequest.Date, multiRequest.StartTime, multiRequest.SubServices.Count);
                    
                    // Get sub-service details from Cosmos DB
                    var subServiceDetails = 
                        await _reservationService.GetMultipleSubServiceDetailsAsync(multiRequest.SubServices);
                    
                    if (subServiceDetails.Count == 0)
                    {
                        _logger.LogWarning("No valid sub-services found for the multi-service request");
                        return new NotFoundObjectResult("No valid sub-services found");
                    }
                    
                    // Create the reservation
                    var (reservation, createSuccess) = 
                        await _reservationService.CreateMultiServiceReservationAsync(
                            multiRequest.Date, 
                            multiRequest.StartTime,
                            subServiceDetails);
                    
                    if (!createSuccess || reservation == null)
                    {
                        _logger.LogError("Failed to create multi-service reservation");
                        return new ObjectResult("Failed to create reservation") { StatusCode = (int)HttpStatusCode.InternalServerError };
                    }
                    
                    // Return response
                    var response = new LockMultipleServicesResponse
                    {
                        Success = true,
                        BookingId = reservation.Id,
                        LockExpiresAt = reservation.LockExpiresAt,
                        StartTime = reservation.StartTime,
                        EndTime = reservation.EndTime,
                        Duration = reservation.Duration,
                        SubServices = reservation.SubServices?.Select(s => new SubServiceResponse
                        {
                            Id = s.Id,
                            Name = s.Name,
                            Duration = s.Duration
                        }).ToList()
                    };
                    
                    _logger.LogInformation("Successfully created multi-service reservation with ID {ReservationId}", reservation.Id);
                    return new OkObjectResult(response);
                }
                
                // If we got here, neither request type was valid
                _logger.LogWarning("Request could not be parsed as either a single or multi-service reservation");
                return new BadRequestObjectResult("Invalid request format");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing lock reservation request");
                return new ObjectResult("An error occurred processing your request") { StatusCode = (int)HttpStatusCode.InternalServerError };
            }
        }
    }
}
