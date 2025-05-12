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
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("DeleteBooking function processing request");

            try
            {
                // Read and deserialize the request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken);
                var deleteRequest = JsonConvert.DeserializeObject<DeleteBookingRequest>(requestBody);
                
                if (deleteRequest == null || string.IsNullOrEmpty(deleteRequest.BookingId))
                {
                    return new BadRequestObjectResult(new DeleteBookingResponse 
                    { 
                        Success = false, 
                        Message = "BookingId is required" 
                    });
                }
                
                // Verify the booking exists
                var booking = await _cosmosDbService.GetItemAsync<Booking>("Bookings", deleteRequest.BookingId);
                if (booking == null)
                {
                    return new NotFoundObjectResult(new DeleteBookingResponse 
                    { 
                        Success = false, 
                        Message = $"Booking with ID {deleteRequest.BookingId} not found" 
                    });
                }
                
                // Delete the booking
                await _cosmosDbService.DeleteItemAsync("Bookings", deleteRequest.BookingId);
                
                _logger.LogInformation($"Successfully deleted booking {deleteRequest.BookingId}");
                
                // Return success response
                return new OkObjectResult(new DeleteBookingResponse
                { 
                    Success = true,
                    BookingId = deleteRequest.BookingId,
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
