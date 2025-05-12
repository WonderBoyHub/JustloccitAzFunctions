using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CreateBooking.Function.Models;
using CreateBooking.Function.Data;
using System.Threading;

namespace Justloccit.Function
{
    public class CreateBooking
    {
        private readonly ILogger<CreateBooking> _logger;
        private readonly CosmosDbService _cosmosDbService;

        public CreateBooking(ILogger<CreateBooking> logger, CosmosDbService cosmosDbService)
        {
            _logger = logger;
            _cosmosDbService = cosmosDbService;
        }

        [Function("CreateBooking")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "createbooking")] HttpRequest req,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("CreateBooking function processing request");

            try
            {
                // Read and deserialize the request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken);
                var bookingRequest = JsonConvert.DeserializeObject<CreateBookingRequest>(requestBody);
                
                if (bookingRequest == null)
                {
                    return new BadRequestObjectResult(new CreateBookingResponse 
                    { 
                        Success = false, 
                        Message = "Invalid request format" 
                    });
                }
                
                // Validate request
                if (string.IsNullOrEmpty(bookingRequest.CustomerId))
                {
                    return new BadRequestObjectResult(new CreateBookingResponse 
                    { 
                        Success = false, 
                        Message = "CustomerId is required" 
                    });
                }
                
                if (string.IsNullOrEmpty(bookingRequest.SubServiceId))
                {
                    return new BadRequestObjectResult(new CreateBookingResponse 
                    { 
                        Success = false, 
                        Message = "SubServiceId is required" 
                    });
                }
                
                // Verify the customer exists
                var customer = await _cosmosDbService.GetItemAsync<dynamic>("Customers", bookingRequest.CustomerId);
                if (customer == null)
                {
                    return new BadRequestObjectResult(new CreateBookingResponse 
                    { 
                        Success = false, 
                        Message = $"Customer with ID {bookingRequest.CustomerId} not found" 
                    });
                }
                
                // Verify the subservice exists
                var subService = await _cosmosDbService.GetItemAsync<dynamic>("SubServices", bookingRequest.SubServiceId);
                if (subService == null)
                {
                    return new BadRequestObjectResult(new CreateBookingResponse 
                    { 
                        Success = false, 
                        Message = $"SubService with ID {bookingRequest.SubServiceId} not found" 
                    });
                }
                
                // Create a new booking
                var booking = new BookingModel
                {
                    Id = Guid.NewGuid().ToString(),
                    CustomerId = bookingRequest.CustomerId,
                    CustomerName = customer.name?.ToString() ?? string.Empty,
                    CustomerEmail = customer.email?.ToString() ?? string.Empty,
                    CustomerPhone = customer.phone?.ToString() ?? string.Empty,
                    SubServiceId = bookingRequest.SubServiceId,
                    SubServiceName = subService.name?.ToString() ?? string.Empty,
                    ServiceId = subService.serviceId?.ToString() ?? string.Empty,
                    ServiceName = subService.serviceName?.ToString() ?? string.Empty,
                    Date = bookingRequest.Date,
                    StartTime = bookingRequest.StartTime,
                    EndTime = bookingRequest.EndTime,
                    Notes = bookingRequest.Notes ?? string.Empty,
                    BookingStatus = BookingStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Create the booking in Cosmos DB
                await _cosmosDbService.CreateItemAsync("Bookings", booking, booking.Id);

                _logger.LogInformation($"Successfully created booking {booking.Id}");
                
                // Return the booking details
                return new OkObjectResult(new CreateBookingResponse
                { 
                    Success = true,
                    BookingId = booking.Id,
                    CustomerId = booking.CustomerId,
                    Date = booking.Date,
                    StartTime = booking.StartTime,
                    EndTime = booking.EndTime
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CreateBooking request");
                return new ObjectResult(new CreateBookingResponse 
                { 
                    Success = false, 
                    Message = "An error occurred while creating the booking" 
                })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }
    }
}
