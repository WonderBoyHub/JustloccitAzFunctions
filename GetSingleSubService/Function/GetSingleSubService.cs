using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;

namespace Justloccit.Function
{
    public class GetSingleSubService
    {
        private readonly ILogger<GetSingleSubService> _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _subServicesContainer;

        public GetSingleSubService(
            CosmosClient cosmosClient,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            ILogger<GetSingleSubService> logger)
        {
            _logger = logger;
            _cosmosClient = cosmosClient;
            
            var servicesDatabase = cosmosClient.GetDatabase(configuration["CosmosDb:ServicesDatabase"]);
            _subServicesContainer = servicesDatabase.GetContainer(configuration["CosmosDb:SubServicesContainer"]);
        }

        [Function("GetSingleSubService")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "subservices/{id}")] HttpRequest req,
            string id)
        {
            _logger.LogInformation("Processing request to get sub-service with ID: {Id}", id);

            if (string.IsNullOrEmpty(id))
            {
                return new BadRequestObjectResult("Sub-service ID is required");
            }

            try
            {
                // Try to retrieve the sub-service by ID
                SubServiceModel subService;
                try
                {
                    var response = await _subServicesContainer.ReadItemAsync<SubServiceModel>(
                        id, 
                        new PartitionKey(id));
                    
                    subService = response.Resource;
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Sub-service with ID {Id} not found", id);
                    return new NotFoundObjectResult($"Sub-service with ID {id} not found");
                }

                _logger.LogInformation("Retrieved sub-service with ID: {Id}", id);
                return new OkObjectResult(subService);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sub-service with ID {Id}", id);
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }

    // Define the model class within the function to keep it isolated
    public class SubServiceModel
    {
        public string Id { get; set; } = string.Empty;
        public string ServiceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Duration { get; set; } = 30;
        public decimal Price { get; set; }
        public bool IsAvailable { get; set; } = true;
    }
}
