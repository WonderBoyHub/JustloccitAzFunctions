using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using System.Collections.Generic;

namespace Justloccit.Function
{
    public class DeleteService
    {
        private readonly ILogger<DeleteService> _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _servicesContainer;
        private readonly Container _subServicesContainer;

        public DeleteService(
            CosmosClient cosmosClient,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            ILogger<DeleteService> logger)
        {
            _logger = logger;
            _cosmosClient = cosmosClient;
            
            var servicesDatabase = cosmosClient.GetDatabase(configuration["CosmosDb:ServicesDatabase"]);
            _servicesContainer = servicesDatabase.GetContainer("ServicesContainer");
            _subServicesContainer = servicesDatabase.GetContainer("SubServicesContainer");
        }

        [Function("DeleteService")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "services/{id}")] HttpRequest req,
            string id)
        {
            _logger.LogInformation("Processing request to delete service with ID: {Id}", id);

            if (string.IsNullOrEmpty(id))
            {
                return new BadRequestObjectResult("Service ID is required");
            }

            try
            {
                // Delete all sub-services associated with this service first
                await DeleteRelatedSubServices(id);

                // Delete the service from Cosmos DB
                await _servicesContainer.DeleteItemAsync<ServiceModel>(
                    id, 
                    new PartitionKey(id));

                _logger.LogInformation("Service deleted successfully with ID: {Id}", id);
                return new NoContentResult();
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Service with ID {Id} not found", id);
                return new NotFoundObjectResult($"Service with ID {id} not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting service with ID {Id}", id);
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        private async Task DeleteRelatedSubServices(string serviceId)
        {
            try
            {
                // Query to find all sub-services for this service
                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.serviceId = @serviceId")
                    .WithParameter("@serviceId", serviceId);

                var subServices = new List<SubServiceModel>();
                using (var iterator = _subServicesContainer.GetItemQueryIterator<SubServiceModel>(query))
                {
                    while (iterator.HasMoreResults)
                    {
                        var response = await iterator.ReadNextAsync();
                        subServices.AddRange(response);
                    }
                }

                _logger.LogInformation("Found {Count} sub-services to delete for service ID: {ServiceId}", 
                    subServices.Count, serviceId);

                // Delete each sub-service
                foreach (var subService in subServices)
                {
                    await _subServicesContainer.DeleteItemAsync<SubServiceModel>(
                        subService.Id, 
                        new PartitionKey(subService.Id));
                    
                    _logger.LogInformation("Deleted sub-service with ID: {Id}", subService.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting related sub-services for service ID: {ServiceId}", serviceId);
                throw;
            }
        }
    }

    // Define the model classes within the function to keep it isolated
    public class ServiceModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class SubServiceModel
    {
        public string Id { get; set; } = string.Empty;
        public string ServiceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
