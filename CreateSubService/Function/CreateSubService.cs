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
    public class CreateSubService
    {
        private readonly ILogger<CreateSubService> _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _subServicesContainer;

        public CreateSubService(
            CosmosClient cosmosClient,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            ILogger<CreateSubService> logger)
        {
            _logger = logger;
            _cosmosClient = cosmosClient;
            
            var servicesDatabase = cosmosClient.GetDatabase(configuration["CosmosDb:ServicesDatabase"]);
            _subServicesContainer = servicesDatabase.GetContainer(configuration["CosmosDb:SubServicesContainer"]);
        }

        [Function("CreateSubService")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req)
        {
            _logger.LogInformation("Processing request to create a new sub-service");

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
                var subService = JsonSerializer.Deserialize<SubServiceModel>(requestBody, options);

                if (subService == null)
                {
                    return new BadRequestObjectResult("Invalid sub-service data format");
                }

                // Validate required fields
                if (string.IsNullOrEmpty(subService.Name) || string.IsNullOrEmpty(subService.ServiceId))
                {
                    return new BadRequestObjectResult("Sub-service name and serviceId are required");
                }

                // Ensure the sub-service has an ID
                if (string.IsNullOrEmpty(subService.Id))
                {
                    subService.Id = Guid.NewGuid().ToString();
                }

                // Create the sub-service in Cosmos DB
                var response = await _subServicesContainer.CreateItemAsync(
                    subService, 
                    new PartitionKey(subService.Id));

                _logger.LogInformation("Sub-service created successfully with ID: {Id}", subService.Id);

                // Return the created sub-service
                return new OkObjectResult(response.Resource);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                _logger.LogError(ex, "Conflict occurred while creating sub-service");
                return new ConflictObjectResult("A sub-service with this ID already exists");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sub-service");
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
