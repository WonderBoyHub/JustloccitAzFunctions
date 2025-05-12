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
    public class GetAllServices
    {
        private readonly ILogger<GetAllServices> _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _servicesContainer;

        public GetAllServices(
            CosmosClient cosmosClient,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            ILogger<GetAllServices> logger)
        {
            _logger = logger;
            _cosmosClient = cosmosClient;
            
            var servicesDatabase = cosmosClient.GetDatabase(configuration["CosmosDb:ServicesDatabase"]);
            _servicesContainer = servicesDatabase.GetContainer(configuration["CosmosDb:ServicesContainer"]);
        }

        [Function("GetAllServices")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "services")] HttpRequest req)
        {
            _logger.LogInformation("Processing request to get all services");

            try
            {
                // Get isAvailable filter from query parameter (optional)
                string isAvailableParam = req.Query["isAvailable"];
                bool? isAvailable = !string.IsNullOrEmpty(isAvailableParam) && bool.TryParse(isAvailableParam, out var parsed) 
                    ? parsed 
                    : null;
                
                // Construct query
                QueryDefinition queryDefinition;
                if (isAvailable.HasValue)
                {
                    _logger.LogInformation("Filtering services by isAvailable: {IsAvailable}", isAvailable.Value);
                    queryDefinition = new QueryDefinition(
                        "SELECT * FROM c WHERE c.isAvailable = @isAvailable")
                        .WithParameter("@isAvailable", isAvailable.Value);
                }
                else
                {
                    queryDefinition = new QueryDefinition("SELECT * FROM c");
                }

                // Execute the query
                var services = new List<ServiceModel>();
                using (var iterator = _servicesContainer.GetItemQueryIterator<ServiceModel>(queryDefinition))
                {
                    while (iterator.HasMoreResults)
                    {
                        var response = await iterator.ReadNextAsync();
                        services.AddRange(response);
                    }
                }

                _logger.LogInformation("Retrieved {Count} services", services.Count);
                return new OkObjectResult(services);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving services");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }

    // Define the model class within the function to keep it isolated
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
