using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UpdateBooking.Function.Models;
using UpdateBooking.Function.Data;
using System.Threading;
using Azure.Messaging.EventGrid;
using Azure;

namespace Justloccit.Function
{
    public class UpdateBooking
    {
        private readonly ILogger<UpdateBooking> _logger;
        private readonly CosmosDbService _cosmosDbService;
        private readonly EventGridPublisherClient _eventGridClient;

        public UpdateBooking(
            ILogger<UpdateBooking> logger, 
            CosmosDbService cosmosDbService,
            EventGridPublisherClient eventGridClient)
        {
            _logger = logger;
            _cosmosDbService = cosmosDbService;
            _eventGridClient = eventGridClient;
        }

        [Function("UpdateBooking")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "updatebooking")] HttpRequest req,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("UpdateBooking function processing request");

            try
            {
                // Read and deserialize the request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken);
                var updateRequest = JsonConvert.DeserializeObject<BookingModel>(requestBody);
                
                if (updateRequest == null)
                {
                    return new BadRequestObjectResult("Invalid request body");
                }
                
                // Get the existing booking
                var booking = await _cosmosDbService.GetItemAsync<BookingModel>("Bookings", updateRequest.Id, updateRequest.CustomerId);
                if (booking == null)
                {
                    return new BadRequestObjectResult("Booking not found");
                }
                
                // Store the previous status to detect changes
                var previousStatus = booking.BookingStatus;
                bool statusChanged = false;
                
                bool customerInfoUpdated = false;
                
                // Update customer if provided
                if (!string.IsNullOrEmpty(updateRequest.CustomerId) && updateRequest.CustomerId != booking.CustomerId)
                {
                    var customer = await _cosmosDbService.GetItemAsync<dynamic>("Customers", updateRequest.CustomerId, updateRequest.CustomerEmail);
                    if (customer == null)
                    {
                        return new BadRequestObjectResult("Could not find customer");
                    }
                    
                    booking.CustomerId = updateRequest.CustomerId;
                    booking.CustomerName = customer.name?.ToString() ?? string.Empty;
                    booking.CustomerEmail = customer.email?.ToString() ?? string.Empty;
                    booking.CustomerPhone = customer.phone?.ToString() ?? string.Empty;
                    customerInfoUpdated = true;
                }
                
                // Update sub-service if provided
                if (!string.IsNullOrEmpty(updateRequest.SubServiceId) && updateRequest.SubServiceId != booking.SubServiceId)
                {
                    var subService = await _cosmosDbService.GetItemAsync<dynamic>("SubServices", updateRequest.SubServiceId, updateRequest.ServiceId);
                    if (subService == null)
                    {
                        return new BadRequestObjectResult("Could not find sub-service");
                    }
                    
                    booking.SubServiceId = updateRequest.SubServiceId;
                    booking.SubServiceName = subService.name?.ToString() ?? string.Empty;
                    booking.ServiceId = subService.serviceId?.ToString() ?? string.Empty;
                    booking.ServiceName = subService.serviceName?.ToString() ?? string.Empty;
                }
                
                // Update other fields if provided
                if (updateRequest.Date.HasValue)
                {
                    booking.Date = updateRequest.Date.Value;
                }
                
                if (updateRequest.StartTime.HasValue)
                {
                    booking.StartTime = updateRequest.StartTime.Value;
                }
                
                if (updateRequest.EndTime.HasValue)
                {
                    booking.EndTime = updateRequest.EndTime.Value;
                }
                
                if (updateRequest.BookingStatus.HasValue && updateRequest.BookingStatus.Value != previousStatus)
                {
                    booking.BookingStatus = updateRequest.BookingStatus.Value;
                    statusChanged = true;
                }
                
                if (updateRequest.Notes != null)
                {
                    booking.Notes = updateRequest.Notes;
                }
                
                // Update timestamp
                booking.UpdatedAt = DateTime.UtcNow;
                
                // Update the booking in Cosmos DB
                var updatedBooking = await _cosmosDbService.UpdateItemAsync("Bookings", booking, booking.Id, booking.CustomerId);
                
                _logger.LogInformation($"Successfully updated booking {booking.Id}");
                
                // Send event if the status changed to Confirmed or Cancelled
                if (statusChanged)
                {
                    if (booking.BookingStatus == BookingStatus.Confirmed)
                    {
                        await PublishBookingConfirmedEventAsync(booking);
                    }
                    else if (booking.BookingStatus == BookingStatus.Cancelled)
                    {
                        await PublishBookingCancelledEventAsync(booking);
                    }
                }
                
                // Return the updated booking details
                return new OkObjectResult(new UpdateBookingResponse
                { 
                    Success = true,
                    BookingId = updatedBooking.Id,
                    CustomerId = updatedBooking.CustomerId,
                    Date = updatedBooking.Date,
                    StartTime = updatedBooking.StartTime,
                    EndTime = updatedBooking.EndTime,
                    BookingStatus = updatedBooking.BookingStatus,
                    Message = "Booking updated successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing UpdateBooking request");
                return new BadRequestObjectResult("An error occurred while updating the booking");
            }
        }
        
        private async Task PublishBookingConfirmedEventAsync(BookingModel booking)
        {
            try
            {
                // Create data for the event
                var eventData = new
                {
                    booking.Id,
                    booking.CustomerId,
                    booking.CustomerName,
                    booking.CustomerEmail,
                    booking.CustomerPhone,
                    booking.SubServiceId,
                    booking.Date,
                    booking.StartTime,
                    booking.EndTime,
                    booking.BookingStatus,
                    EventType = "BookingConfirmed"
                };
                
                // Create and publish the event
                var eventGridEvent = new EventGridEvent(
                    subject: $"booking/{booking.Id}",
                    eventType: "Justloccit.Booking.Confirmed",
                    dataVersion: "1.0",
                    data: BinaryData.FromObjectAsJson(eventData));
                
                await _eventGridClient.SendEventAsync(eventGridEvent);
                
                _logger.LogInformation($"Published BookingConfirmed event for booking {booking.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error publishing BookingConfirmed event for booking {booking.Id}");
                // Continue with function execution even if event publishing fails
            }
        }
        
        private async Task PublishBookingCancelledEventAsync(BookingModel booking)
        {
            try
            {
                // Create data for the event
                var eventData = new
                {
                    booking.Id,
                    booking.CustomerId,
                    booking.CustomerName,
                    booking.CustomerEmail,
                    booking.CustomerPhone,
                    booking.SubServiceId,
                    booking.Date,
                    booking.StartTime,
                    booking.EndTime,
                    booking.BookingStatus,
                    EventType = "BookingCancelled"
                };
                
                // Create and publish the event
                var eventGridEvent = new EventGridEvent(
                    subject: $"booking/{booking.Id}",
                    eventType: "Justloccit.Booking.Cancelled",
                    dataVersion: "1.0",
                    data: BinaryData.FromObjectAsJson(eventData));
                
                await _eventGridClient.SendEventAsync(eventGridEvent);
                
                _logger.LogInformation($"Published BookingCancelled event for booking {booking.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error publishing BookingCancelled event for booking {booking.Id}");
                // Continue with function execution even if event publishing fails
            }
        }
    }
}
