using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Threading;
using ConfirmBookingAsync.Function.Services;
using ConfirmBookingAsync.Function.Models;

namespace ConfirmBookingAsync.Function
{
    public class ConfirmBookingAsyncFunction
    {
        private readonly ILogger<ConfirmBookingAsyncFunction> _logger;
        private readonly ReservationService _reservationService;

        public ConfirmBookingAsyncFunction(
            ILogger<ConfirmBookingAsyncFunction> logger,
            ReservationService reservationService)
        {
            _logger = logger;
            _reservationService = reservationService;
        }

        [Function("ConfirmBookingAsync")]
        public async Task<IActionResult> ConfirmBooking(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, CancellationToken token)
        {
            _logger.LogInformation("ConfirmBookingAsync function processing request");

            try
            {
                // Read request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<dynamic>(requestBody);
                
                string bookingId = data?.bookingId;
                var customer = JsonConvert.DeserializeObject<Customer>(data?.customer.ToString());
                
                if (string.IsNullOrEmpty(bookingId))
                {
                    return new BadRequestObjectResult("bookingId is required");
                }
                
                if (customer == null || string.IsNullOrEmpty(customer.Id))
                {
                    return new BadRequestObjectResult("Valid customer information is required");
                }
                
                // Confirm the booking using the provided cancellation token
                var confirmedBooking = await _reservationService.ConfirmBookingAsync(
                    bookingId,
                    customer,
                    token);
                
                _logger.LogInformation($"Successfully confirmed booking {confirmedBooking.Id} from reservation {bookingId}");
                
                // Return the confirmed booking details
                return new OkObjectResult(new 
                { 
                    success = true,
                    bookingId = confirmedBooking.Id,
                    customerId = confirmedBooking.CustomerId,
                    date = confirmedBooking.Date,
                    startTime = confirmedBooking.StartTime,
                    endTime = confirmedBooking.EndTime
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, $"Failed to confirm booking: {ex.Message}");
                return new BadRequestObjectResult(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ConfirmBookingAsync request");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
} 