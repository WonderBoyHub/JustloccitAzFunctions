using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GetAllServicesWithSubServices.Function.Data
{
    public class CosmosDbService
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Dictionary<string, Container> _containers;
        private readonly ILogger<CosmosDbService> _logger;
        private readonly IConfiguration _configuration;

        public CosmosDbService(
            CosmosClient cosmosClient, 
            IConfiguration configuration,
            ILogger<CosmosDbService> logger)
        {
            _cosmosClient = cosmosClient;
            _configuration = configuration;
            _logger = logger;
            _containers = new Dictionary<string, Container>();

            InitializeContainers();
        }

        private void InitializeContainers()
        {
            try
            {
                var servicesDatabase = _configuration["CosmosDb:ServiceDB"]!;
                
                var databases = new[]
                {
                    new { Database = servicesDatabase, Container = _configuration["CosmosDb:Services"]! },
                    new { Database = servicesDatabase, Container = _configuration["CosmosDb:Subservices"]! }
                };

                foreach (var db in databases)
                {
                    _logger.LogInformation("Initializing container: Database={Database}, Container={Container}", 
                        db.Database, db.Container);
                        
                    var container = _cosmosClient.GetContainer(db.Database, db.Container);
                    _containers[db.Container] = container;
                }
                
                _logger.LogInformation("Successfully initialized CosmosDB containers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize CosmosDB containers");
                throw;
            }
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
                _logger.LogWarning("Item not found: Container={Container}, Id={Id}", containerName, id);
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving item: Container={Container}, Id={Id}", containerName, id);
                throw;
            }
        }

        public async Task<IEnumerable<T>> GetItemsAsync<T>(string containerName, string query)
        {
            try
            {
                _logger.LogInformation("Executing query: Container={Container}, Query={Query}", containerName, query);
                
                var container = _containers[containerName];
                var queryDefinition = new QueryDefinition(query);
                var iterator = container.GetItemQueryIterator<T>(queryDefinition);

                var results = new List<T>();
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response.Resource);
                }

                _logger.LogInformation("Query returned {Count} results", results.Count);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing query: Container={Container}, Query={Query}", containerName, query);
                throw;
            }
        }
        
        public async Task<T> CreateItemAsync<T>(string containerName, T item, string id)
        {
            try
            {
                var container = _containers[containerName];
                var response = await container.CreateItemAsync(item, new PartitionKey(id));
                _logger.LogInformation("Created item: Container={Container}, Id={Id}", containerName, id);
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating item: Container={Container}, Id={Id}", containerName, id);
                throw;
            }
        }

        public async Task<T> UpdateItemAsync<T>(string containerName, T item, string id)
        {
            try
            {
                var container = _containers[containerName];
                var response = await container.ReplaceItemAsync(item, id, new PartitionKey(id));
                _logger.LogInformation("Updated item: Container={Container}, Id={Id}", containerName, id);
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating item: Container={Container}, Id={Id}", containerName, id);
                throw;
            }
        }
    }
} 