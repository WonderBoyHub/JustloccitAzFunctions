using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using DeleteBooking.Function.Models;
using DeleteBooking.Function.Data;
using System.Threading;

namespace Justloccit.Function
{
    public class DeleteBooking
    {
        private readonly ILogger<DeleteBooking> _logger;
        private readonly CosmosDbService _cosmosDbService;

        public DeleteBooking(ILogger<DeleteBooking> logger, CosmosDbService cosmosDbService)
        {
            _logger = logger;
            _cosmosDbService = cosmosDbService;
        }

        [Function("DeleteBooking")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "deletebooking")] HttpRequest req,
            BookingModel booking,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("DeleteBooking function processing request");

            try
            {
                // Verify the booking exists
                var booking = await _cosmosDbService.GetItemAsync<BookingModel>("Bookings", booking.Id, booking.CustomerId);
                if (booking == null)
                {
                    return new NotFoundObjectResult(new DeleteBookingResponse 
                    { 
                        Success = false, 
                        Message = $"Booking with ID {booking.Id} not found" 
                    });
                }
                
                // Delete the booking
                await _cosmosDbService.DeleteItemAsync("Bookings", booking.Id, booking.CustomerId);
                
                _logger.LogInformation($"Successfully deleted booking {booking.Id}");
                
                // Return success response
                return new OkObjectResult(new DeleteBookingResponse
                { 
                    Success = true,
                    BookingId = booking.Id,
                    Message = "Booking deleted successfully"
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Failed to delete booking: {Message}", ex.Message);
                return new NotFoundObjectResult(new DeleteBookingResponse 
                { 
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing DeleteBooking request");
                return new ObjectResult(new DeleteBookingResponse 
                { 
                    Success = false, 
                    Message = "An error occurred while deleting the booking" 
                })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }
    }
}
