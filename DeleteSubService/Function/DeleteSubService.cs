using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;

namespace Justloccit.Function
{
    public class DeleteSubService
    {
        private readonly ILogger<DeleteSubService> _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _subServicesContainer;

        public DeleteSubService(
            CosmosClient cosmosClient,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            ILogger<DeleteSubService> logger)
        {
            _logger = logger;
            _cosmosClient = cosmosClient;
            
            var servicesDatabase = cosmosClient.GetDatabase(configuration["CosmosDb:ServicesDatabase"]);
            _subServicesContainer = servicesDatabase.GetContainer(configuration["CosmosDb:SubServicesContainer"]);
        }

        [Function("DeleteSubService")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "subservices/{id}")] HttpRequest req,
            string id)
        {
            _logger.LogInformation("Processing request to delete sub-service with ID: {Id}", id);

            if (string.IsNullOrEmpty(id))
            {
                return new BadRequestObjectResult("Sub-service ID is required");
            }

            try
            {
                // Delete the sub-service from Cosmos DB
                await _subServicesContainer.DeleteItemAsync<SubServiceModel>(
                    id, 
                    new PartitionKey(id));

                _logger.LogInformation("Sub-service deleted successfully with ID: {Id}", id);
                return new NoContentResult();
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Sub-service with ID {Id} not found", id);
                return new NotFoundObjectResult($"Sub-service with ID {id} not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting sub-service with ID {Id}", id);
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
