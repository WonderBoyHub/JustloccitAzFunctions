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
    public class GetAllSubServices
    {
        private readonly ILogger<GetAllSubServices> _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _subServicesContainer;

        public GetAllSubServices(
            CosmosClient cosmosClient,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            ILogger<GetAllSubServices> logger)
        {
            _logger = logger;
            _cosmosClient = cosmosClient;
            
            var servicesDatabase = cosmosClient.GetDatabase(configuration["CosmosDb:ServicesDatabase"]);
            _subServicesContainer = servicesDatabase.GetContainer(configuration["CosmosDb:SubServicesContainer"]);
        }

        [Function("GetAllSubServices")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "subservices")] HttpRequest req)
        {
            _logger.LogInformation("Processing request to get all sub-services");

            try
            {
                // Get service ID filter from query parameter (optional)
                string serviceId = req.Query["serviceId"];
                
                // Construct query
                QueryDefinition queryDefinition;
                if (!string.IsNullOrEmpty(serviceId))
                {
                    _logger.LogInformation("Filtering sub-services by service ID: {ServiceId}", serviceId);
                    queryDefinition = new QueryDefinition(
                        "SELECT * FROM c WHERE c.serviceId = @serviceId")
                        .WithParameter("@serviceId", serviceId);
                }
                else
                {
                    queryDefinition = new QueryDefinition("SELECT * FROM c");
                }

                // Execute the query
                var subServices = new List<SubServiceModel>();
                using (var iterator = _subServicesContainer.GetItemQueryIterator<SubServiceModel>(queryDefinition))
                {
                    while (iterator.HasMoreResults)
                    {
                        var response = await iterator.ReadNextAsync();
                        subServices.AddRange(response);
                    }
                }

                _logger.LogInformation("Retrieved {Count} sub-services", subServices.Count);
                return new OkObjectResult(subServices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sub-services");
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
