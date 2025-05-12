using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GetBookingsByDateRange.Function.Data
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

            var bookingsDatabase = cosmosClient.GetDatabase(configuration["CosmosDb:JustloccitBookings"]!);
            var bookingsContainer = bookingsDatabase.GetContainer(configuration["CosmosDb:BookingsContainer"]!);

            _containers = new Dictionary<string, Container>
            {
                { "Bookings", bookingsContainer }
            };
            _logger.LogInformation("CosmosDbService initialized with containers: {Containers}", string.Join(", ", _containers.Keys));
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
    }
} 