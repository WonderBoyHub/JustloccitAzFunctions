using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConfirmBookingAsync.Function.Data
{
    public class CosmosDbService
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Dictionary<string, Container> _containers;
        private readonly Dictionary<string, Database> _databases;
        private readonly Container _timeslotsContainer;
        private readonly Container _customersContainer;
        private readonly Container _reservationsContainer;
        private readonly ILogger<CosmosDbService> _logger;

        public CosmosDbService(
            CosmosClient cosmosClient,
            IConfiguration configuration,
            ILogger<CosmosDbService> logger)
            {
                _logger = logger;
                _cosmosClient = cosmosClient;

                var timeslotsDatabase = cosmosClient.GetDatabase(configuration["CosmosDb:JustloccitBookings"]!);
                _timeslotsContainer = timeslotsDatabase.GetContainer(configuration["CosmosDb:BookingsContainer"]!);
                var customersDatabase = cosmosClient.GetDatabase(configuration["CosmosDb:JustloccitCustomers"]!);
                _customersContainer = customersDatabase.GetContainer(configuration["CosmosDb:CustomersContainer"]!);
                var reservationsDatabase = cosmosClient.GetDatabase(configuration["CosmosDb:JustloccitReservations"]!);
                _reservationsContainer = reservationsDatabase.GetContainer(configuration["CosmosDb:ReservationsContainer"]!);
                _containers = new Dictionary<string, Container>
                {
                    { "Bookings", _timeslotsContainer },
                    { "Customers", _customersContainer },
                    { "Reservations", _reservationsContainer }
                };
                _logger.LogInformation("CosmosDbService initialized with containers: {Containers}", string.Join(", ", _containers.Keys));
                _databases = new Dictionary<string, Database>
                {
                    { "Bookings", timeslotsDatabase },
                    { "Customers", customersDatabase },
                    { "Reservations", reservationsDatabase }
                };
            }

        public async Task<T?> GetItemAsync<T>(string containerName, string id)
        {
            try
            {
                var container = _containers[containerName];
                var response = await container.ReadItemAsync<T>(id, new PartitionKey(id));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving item with ID {Id} from container {Container}", id, containerName);
                throw;
            }
        }

        public async Task<IEnumerable<T>> GetItemsAsync<T>(string containerName, string query)
        {
            try
            {
                var container = _containers[containerName];
                var queryDefinition = new QueryDefinition(query);
                var iterator = container.GetItemQueryIterator<T>(queryDefinition);

                var results = new List<T>();
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response.Resource);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing query on container {Container}: {Query}", containerName, query);
                throw;
            }
        }

        public async Task<T> CreateItemAsync<T>(string containerName, T item, string id)
        {
            try
            {
                var container = _containers[containerName];
                var response = await container.CreateItemAsync(item, new PartitionKey(id));
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating item with ID {Id} in container {Container}", id, containerName);
                throw;
            }
        }

        public async Task<T> UpdateItemAsync<T>(string containerName, T item, string id)
        {
            try
            {
                var container = _containers[containerName];
                var response = await container.ReplaceItemAsync(item, id, new PartitionKey(id));
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating item with ID {Id} in container {Container}", id, containerName);
                throw;
            }
        }

        public async Task DeleteItemAsync(string containerName, string id)
        {
            try
            {
                var container = _containers[containerName];
                await container.DeleteItemAsync<object>(id, new PartitionKey(id));
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Attempted to delete non-existent item with ID {Id} from container {Container}", id, containerName);
                // Item doesn't exist, so deletion is effectively complete
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting item with ID {Id} from container {Container}", id, containerName);
                throw;
            }
        }
    }
} 