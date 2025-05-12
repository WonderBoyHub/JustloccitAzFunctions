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
    public class UpdateSubService
    {
        private readonly ILogger<UpdateSubService> _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _subServicesContainer;

        public UpdateSubService(
            CosmosClient cosmosClient,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            ILogger<UpdateSubService> logger)
        {
            _logger = logger;
            _cosmosClient = cosmosClient;
            
            var servicesDatabase = cosmosClient.GetDatabase(configuration["CosmosDb:ServicesDatabase"]);
            _subServicesContainer = servicesDatabase.GetContainer("SubServicesContainer");
        }

        [Function("UpdateSubService")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "subservices/{id}")] HttpRequest req,
            string id)
        {
            _logger.LogInformation("Processing request to update sub-service with ID: {Id}", id);

            if (string.IsNullOrEmpty(id))
            {
                return new BadRequestObjectResult("Sub-service ID is required");
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
                var subService = JsonSerializer.Deserialize<SubServiceModel>(requestBody, options);

                if (subService == null)
                {
                    return new BadRequestObjectResult("Invalid sub-service data format");
                }

                // Ensure the ID in the path matches the ID in the body
                if (subService.Id != id)
                {
                    subService.Id = id;
                }

                // Check if the sub-service exists
                try
                {
                    await _subServicesContainer.ReadItemAsync<SubServiceModel>(id, new PartitionKey(id));
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new NotFoundObjectResult($"Sub-service with ID {id} not found");
                }

                // Update the sub-service in Cosmos DB
                var response = await _subServicesContainer.ReplaceItemAsync(
                    subService,
                    id, 
                    new PartitionKey(id));

                _logger.LogInformation("Sub-service updated successfully with ID: {Id}", id);

                // Return the updated sub-service
                return new OkObjectResult(response.Resource);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating sub-service with ID {Id}", id);
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
