using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using GetAllServicesWithSubServices.Function.Models;
using GetAllServicesWithSubServices.Function.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GetAllServicesWithSubServices.Function
{
    public class GetAllServicesWithSubServicesFunction
    {
        private readonly ILogger<GetAllServicesWithSubServicesFunction> _logger;
        private readonly CosmosDbService _cosmosDbService;

        public GetAllServicesWithSubServicesFunction(
            ILogger<GetAllServicesWithSubServicesFunction> logger,
            CosmosDbService cosmosDbService)
        {
            _logger = logger;
            _cosmosDbService = cosmosDbService;
        }

        [Function("GetAllServicesWithSubServices")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req, CancellationToken token)
        {
            try
            {
                _logger.LogInformation("Getting all services with their sub-services");

                // Get all services
                var services = await _cosmosDbService.GetItemsAsync<ServiceModel>("ServicesContainer", "SELECT * FROM c WHERE c.isActive = true");
                var servicesList = services.ToList();

                // Get all sub-services
                var subServices = await _cosmosDbService.GetItemsAsync<SubServiceModel>("SubServicesContainer", """
                                                    SELECT *
                                                    FROM c
                                                    WHERE c.isAvailable = true
                                                    AND c.serviceId != null
                                                    AND c.serviceId != ''
                                                    """);
                var subServicesList = subServices.ToList();

                // Build result with nested sub-services
                var result = new List<ServiceModel>();
                foreach (var service in servicesList)
                {
                    // Find all sub-services for this service
                    var serviceSubServices = subServicesList
                        .Where(ss => ss.ServiceId == service.Id)
                        .ToList();

                    var serviceWithSubs = new ServiceModel
                    {
                        Id = service.Id,
                        Name = service.Name,
                        Description = service.Description,
                        IsActive = service.IsActive,
                        CreatedAt = service.CreatedAt,
                        UpdatedAt = service.UpdatedAt,
                        SubServices = serviceSubServices
                    };

                    result.Add(serviceWithSubs);
                }

                return new OkObjectResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting services with sub-services");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}