using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.Azure.Cosmos;

namespace Justloccit.Function
{
    public class CreateService
    {
        private readonly ILogger<CreateService> _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _servicesContainer;

        public CreateService(
            CosmosClient cosmosClient,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            ILogger<CreateService> logger)
        {
            _logger = logger;
            _cosmosClient = cosmosClient;
            
            var servicesDatabase = cosmosClient.GetDatabase(configuration["CosmosDb:ServicesDatabase"]);
            _servicesContainer = servicesDatabase.GetContainer(configuration["CosmosDb:ServicesContainer"]);
        }

        [Function("CreateService")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "services")] HttpRequest req)
        {
            _logger.LogInformation("Processing request to create a new service");

            try
            {
                // Read request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                if (string.IsNullOrEmpty(requestBody))
                {
                    return new BadRequestObjectResult("Request body cannot be empty");
                }

                // Deserialize the request
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var service = JsonSerializer.Deserialize<ServiceModel>(requestBody, options);

                if (service == null)
                {
                    return new BadRequestObjectResult("Invalid service data format");
                }

                // Validate required fields
                if (string.IsNullOrEmpty(service.Name))
                {
                    return new BadRequestObjectResult("Service name is required");
                }

                // Ensure the service has an ID
                if (string.IsNullOrEmpty(service.Id))
                {
                    service.Id = Guid.NewGuid().ToString();
                }

                // Set timestamps
                service.CreatedAt = DateTime.UtcNow;
                service.UpdatedAt = DateTime.UtcNow;
                
                // Initialize empty collection if null
                if (service.SubServices == null)
                {
                    service.SubServices = new System.Collections.Generic.List<SubServiceModel>();
                }

                // Create the service in Cosmos DB
                var response = await _servicesContainer.CreateItemAsync(
                    service, 
                    new PartitionKey(service.Id));

                _logger.LogInformation("Service created successfully with ID: {Id}", service.Id);

                // Return the created service
                return new OkObjectResult(response.Resource);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                _logger.LogError(ex, "Conflict occurred while creating service");
                return new ConflictObjectResult("A service with this ID already exists");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating service: {Message}", ex.Message);
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
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int Duration { get; set; } = 30;
        public decimal Price { get; set; }
        public bool IsAvailable { get; set; } = true;
        public System.Collections.Generic.ICollection<SubServiceModel> SubServices { get; set; } = new System.Collections.Generic.List<SubServiceModel>();
    }

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
