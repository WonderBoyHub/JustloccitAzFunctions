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
    public class UpdateService
    {
        private readonly ILogger<UpdateService> _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _servicesContainer;

        public UpdateService(
            CosmosClient cosmosClient,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            ILogger<UpdateService> logger)
        {
            _logger = logger;
            _cosmosClient = cosmosClient;
            
            var servicesDatabase = cosmosClient.GetDatabase(configuration["CosmosDb:ServicesDatabase"]);
            _servicesContainer = servicesDatabase.GetContainer("ServicesContainer");
        }

        [Function("UpdateService")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "services/{id}")] HttpRequest req,
            string id)
        {
            _logger.LogInformation("Processing request to update service with ID: {Id}", id);

            if (string.IsNullOrEmpty(id))
            {
                return new BadRequestObjectResult("Service ID is required");
            }

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

                // Ensure the ID in the path matches the ID in the body
                if (service.Id != id)
                {
                    service.Id = id;
                }

                // Update the service's timestamp
                service.UpdatedAt = DateTime.UtcNow;

                // Check if the service exists
                try
                {
                    await _servicesContainer.ReadItemAsync<ServiceModel>(id, new PartitionKey(id));
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new NotFoundObjectResult($"Service with ID {id} not found");
                }

                // Update the service in Cosmos DB
                var response = await _servicesContainer.ReplaceItemAsync(
                    service,
                    id,
                    new PartitionKey(id));

                _logger.LogInformation("Service updated successfully with ID: {Id}", id);

                // Return the updated service
                return new OkObjectResult(response.Resource);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating service with ID {Id}", id);
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
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int Duration { get; set; } = 30;
        public decimal Price { get; set; }
        public bool IsAvailable { get; set; } = true;
    }
}
