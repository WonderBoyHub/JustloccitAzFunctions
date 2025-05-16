using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using GetBooking.Function.Models;
using GetBooking.Function.Data;
using System.Threading;

namespace Justloccit.Function
{
    public class GetBooking
    {
        private readonly ILogger<GetBooking> _logger;
        private readonly CosmosDbService _cosmosDbService;

        public GetBooking(ILogger<GetBooking> logger, CosmosDbService cosmosDbService)
        {
            _logger = logger;
            _cosmosDbService = cosmosDbService;
        }

        [Function("GetBooking")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "getbooking")] HttpRequest req,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("GetBooking function processing request");

            try
            {
                string bookingId;
                
                // Check if the request is GET or POST
                if (HttpMethods.IsGet(req.Method))
                {
                    // Get the bookingId from query parameter
                    bookingId = req.Query["bookingId"];
                }
                else
                {
                    // Read and deserialize the request body for POST
                    string requestBody = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken);
                    var getRequest = JsonConvert.DeserializeObject<GetBookingRequest>(requestBody);
                    bookingId = getRequest?.BookingId ?? string.Empty;
                }
                
                if (string.IsNullOrEmpty(bookingId))
                {
                    return new BadRequestObjectResult(new GetBookingResponse 
                    { 
                        Success = false, 
                        Message = "BookingId is required" 
                    });
                }
                
                // Get the booking from the database
                var booking = await _cosmosDbService.GetItemAsync<BookingModel>("Bookings", bookingId);
                if (booking == null)
                {
                    return new NotFoundObjectResult(new GetBookingResponse 
                    { 
                        Success = false, 
                        Message = $"Booking with ID {bookingId} not found" 
                    });
                }
                
                // Map the booking entity to booking DTO
                var bookingDto = new BookingModel
                {
                    Id = booking.Id,
                    CustomerId = booking.CustomerId,
                    CustomerName = booking.CustomerName,
                    CustomerEmail = booking.CustomerEmail,
                    CustomerPhone = booking.CustomerPhone,
                    ServiceId = booking.ServiceId,
                    ServiceName = booking.ServiceName,
                    SubServiceId = booking.SubServiceId,
                    SubServiceName = booking.SubServiceName,
                    Date = booking.Date,
                    StartTime = booking.StartTime,
                    EndTime = booking.EndTime,
                    BookingStatus = booking.BookingStatus,
                    Notes = booking.Notes,
                    CreatedAt = booking.CreatedAt,
                    UpdatedAt = booking.UpdatedAt
                };
                
                _logger.LogInformation($"Successfully retrieved booking {bookingId}");
                
                // Return the booking
                return new OkObjectResult(new GetBookingResponse
                { 
                    Success = true,
                    Booking = bookingDto
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing GetBooking request");
                return new ObjectResult(new GetBookingResponse 
                { 
                    Success = false, 
                    Message = "An error occurred while retrieving the booking" 
                })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }
    }
}