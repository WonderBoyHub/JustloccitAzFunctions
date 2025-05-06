using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Justloccit.Function.Models;
using Justloccit.Function.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Justloccit.Function
{
    public class ReleaseReservation
    {
        private readonly ILogger<ReleaseReservation> _logger;
        private readonly ReservationService _reservationService;

        public ReleaseReservation(ILogger<ReleaseReservation> logger, ReservationService reservationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _reservationService = reservationService ?? throw new ArgumentNullException(nameof(reservationService));
        }

        [Function("ReleaseLockedBookingAsync")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "ReleaseLockedBookingAsync")] HttpRequest req)
        {
            _logger.LogInformation("Processing release reservation request");

            if (req == null)
            {
                _logger.LogError("Request object is null");
                return new BadRequestObjectResult("Invalid request");
            }

            try
            {
                // Read request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                
                if (string.IsNullOrEmpty(requestBody))
                {
                    _logger.LogWarning("Empty request body received");
                    return new BadRequestObjectResult("Request body cannot be empty");
                }
                
                _logger.LogDebug("Parsing request body");
                
                ReleaseReservationRequest request;
                try
                {
                    request = JsonSerializer.Deserialize<ReleaseReservationRequest>(requestBody, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize request body");
                    return new BadRequestObjectResult("Invalid request format");
                }
                
                // Validate request
                if (request == null || string.IsNullOrEmpty(request.BookingId) || string.IsNullOrEmpty(request.Date))
                {
                    _logger.LogWarning("Invalid request: BookingId={BookingId}, Date={Date}", 
                        request?.BookingId ?? "(null)", request?.Date ?? "(null)");
                    return new BadRequestObjectResult("Invalid request. BookingId and Date are required.");
                }
                
                _logger.LogInformation("Releasing reservation with ID {BookingId} for date {Date}", request.BookingId, request.Date);
                
                // Release the reservation
                bool success = await _reservationService.ReleaseReservationAsync(request.BookingId, request.Date);
                
                if (success)
                {
                    var response = new ReleaseReservationResponse
                    {
                        Success = true,
                        Message = "Reservation released successfully"
                    };
                    
                    _logger.LogInformation("Successfully released reservation {BookingId}", request.BookingId);
                    return new OkObjectResult(response);
                }
                else
                {
                    var response = new ReleaseReservationResponse
                    {
                        Success = false,
                        Message = "Reservation not found or could not be released"
                    };
                    
                    _logger.LogWarning("Failed to release reservation {BookingId}: not found or could not be released", request.BookingId);
                    return new NotFoundObjectResult(response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing release reservation request");
                return new ObjectResult("An error occurred processing your request") 
                { 
                    StatusCode = (int)HttpStatusCode.InternalServerError 
                };
            }
        }
    }
}
