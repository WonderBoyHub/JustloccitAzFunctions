using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;

namespace Justloccit.Function
{
    public class GetSingleService
    {
        private readonly ILogger<GetSingleService> _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _servicesContainer;
        private readonly Container _subServicesContainer;

        public GetSingleService(
            CosmosClient cosmosClient,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            ILogger<GetSingleService> logger)
        {
            _logger = logger;
            _cosmosClient = cosmosClient;
            
            var servicesDatabase = cosmosClient.GetDatabase(configuration["CosmosDb:ServicesDatabase"]);
            _servicesContainer = servicesDatabase.GetContainer(configuration["CosmosDb:ServicesContainer"]);
            _subServicesContainer = servicesDatabase.GetContainer(configuration["CosmosDb:SubServicesContainer"]);
        }

        [Function("GetSingleService")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "services/{id}")] HttpRequest req,
            string id)
        {
            _logger.LogInformation("Processing request to get service with ID: {Id}", id);

            if (string.IsNullOrEmpty(id))
            {
                return new BadRequestObjectResult("Service ID is required");
            }

            try
            {
                // Try to retrieve the service by ID
                ServiceModel service;
                try
                {
                    var response = await _servicesContainer.ReadItemAsync<ServiceModel>(
                        id, 
                        new PartitionKey(id));
                    
                    service = response.Resource;
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Service with ID {Id} not found", id);
                    return new NotFoundObjectResult($"Service with ID {id} not found");
                }

                // Check if we should include sub-services
                string includeParam = req.Query["includeSubServices"];
                bool includeSubServices = !string.IsNullOrEmpty(includeParam) && 
                    (includeParam.ToLower() == "true" || includeParam == "1");

                if (includeSubServices)
                {
                    // Query to find all sub-services for this service
                    var query = new QueryDefinition(
                        "SELECT * FROM c WHERE c.serviceId = @serviceId")
                        .WithParameter("@serviceId", id);

                    var subServices = new List<SubServiceModel>();
                    using (var iterator = _subServicesContainer.GetItemQueryIterator<SubServiceModel>(query))
                    {
                        while (iterator.HasMoreResults)
                        {
                            var response = await iterator.ReadNextAsync();
                            subServices.AddRange(response);
                        }
                    }

                    // Add sub-services to the service object
                    service.SubServices = subServices;
                    
                    _logger.LogInformation("Retrieved {Count} sub-services for service ID: {Id}", 
                        subServices.Count, id);
                }

                _logger.LogInformation("Retrieved service with ID: {Id}", id);
                return new OkObjectResult(service);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving service with ID {Id}", id);
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }

    // Define the model classes within the function to keep it isolated
    public class ServiceModel
    {
        public string Id { get; set; } = string.Empty;
        public string IsActive { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int Duration { get; set; }
        public decimal Price { get; set; }
        public bool IsAvailable { get; set; }
        public ICollection<SubServiceModel> SubServices { get; set; } = new List<SubServiceModel>();
    }

    public class SubServiceModel
    {
        public string Id { get; set; } = string.Empty;
        public string ServiceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Duration { get; set; }
        public decimal Price { get; set; }
        public bool IsAvailable { get; set; }
    }
}
