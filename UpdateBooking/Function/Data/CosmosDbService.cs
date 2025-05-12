using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UpdateBooking.Function.Data
{
    public class CosmosDbService
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Dictionary<string, Container> _containers;
        private readonly ILogger<CosmosDbService> _logger;

        public CosmosDbService(
            CosmosClient cosmosClient,
            IConfiguration configuration,
            ILogger<CosmosDbService> logger)
        {
            _logger = logger;
            _cosmosClient = cosmosClient;

            var bookingsDatabase = cosmosClient.GetDatabase(configuration["CosmosDb:BookingsDatabase"]!);
            var bookingsContainer = bookingsDatabase.GetContainer(configuration["CosmosDb:BookingsContainer"]!);
            var customersDatabase = cosmosClient.GetDatabase(configuration["CosmosDb:CustomersDatabase"]!);
            var customersContainer = customersDatabase.GetContainer(configuration["CosmosDb:CustomersContainer"]!);
            var servicesDatabase = cosmosClient.GetDatabase(configuration["CosmosDb:ServicesDatabase"]!);
            var subServicesContainer = servicesDatabase.GetContainer(configuration["CosmosDb:SubServicesContainer"]!);

            _containers = new Dictionary<string, Container>
            {
                { "Bookings", bookingsContainer },
                { "Customers", customersContainer },
                { "SubServices", subServicesContainer }
            };
            _logger.LogInformation("CosmosDbService initialized with containers: {Containers}", string.Join(", ", _containers.Keys));
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
    }
} 