using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Justloccit.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Justloccit.Services
{
    public interface ICosmosDbService
    {
        Task<TimeslotDocument?> GetTimeslotAsync(string id, string partitionKey);
        Task<TimeslotDocument> CreateTimeslotAsync(TimeslotDocument document);
        Task<TimeslotDocument> UpdateTimeslotAsync(TimeslotDocument document);
        Task DeleteTimeslotAsync(string id, string partitionKey);
        Task<IEnumerable<TimeslotDocument>> GetTimeslotsBeforeDateAsync(string date);
    }

    public class CosmosDbService : ICosmosDbService
    {
        private readonly Container _timeslotsContainer;
        private readonly ILogger<CosmosDbService> _logger;

        public CosmosDbService(
            CosmosClient cosmosClient,
            IConfiguration configuration,
            ILogger<CosmosDbService> logger)
        {
            _logger = logger;

            var timeslotsDatabase = cosmosClient.GetDatabase(configuration["CosmosDb:TimeslotsDatabase"])!;
            _timeslotsContainer = timeslotsDatabase.GetContainer(configuration["CosmosDb:TimeslotsContainer"])!;
        }

        public async Task<TimeslotDocument?> GetTimeslotAsync(string id, string partitionKey)
        {
            try
            {
                var response = await _timeslotsContainer.ReadItemAsync<TimeslotDocument>(id, new PartitionKey(partitionKey));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving timeslot with ID {Id}", id);
                throw;
            }
        }

        public async Task<TimeslotDocument> CreateTimeslotAsync(TimeslotDocument document)
        {
            try
            {
                var response = await _timeslotsContainer.CreateItemAsync(document, new PartitionKey(document.PartitionKey));
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating timeslot with ID {Id}", document.Id);
                throw;
            }
        }

        public async Task<TimeslotDocument> UpdateTimeslotAsync(TimeslotDocument document)
        {
            try
            {
                var response = await _timeslotsContainer.ReplaceItemAsync(document, document.Id, new PartitionKey(document.PartitionKey));
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating timeslot with ID {Id}", document.Id);
                throw;
            }
        }

        public async Task DeleteTimeslotAsync(string id, string partitionKey)
        {
            try
            {
                await _timeslotsContainer.DeleteItemAsync<TimeslotDocument>(id, new PartitionKey(partitionKey));
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Timeslot with ID {Id} not found for deletion", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting timeslot with ID {Id}", id);
                throw;
            }
        }

        public async Task<IEnumerable<TimeslotDocument>> GetTimeslotsBeforeDateAsync(string date)
        {
            try
            {
                var query = new QueryDefinition("SELECT * FROM c WHERE c.date < @date")
                    .WithParameter("@date", date);

                var results = new List<TimeslotDocument>();
                using var iterator = _timeslotsContainer.GetItemQueryIterator<TimeslotDocument>(query);
                
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response.ToList());
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying timeslots before date {Date}", date);
                throw;
            }
        }
    }
} 