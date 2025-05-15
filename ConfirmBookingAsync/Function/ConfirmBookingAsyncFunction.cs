using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ConfirmBookingAsync.Function.Models;
using ConfirmBookingAsync.Function.Data;
using System.Threading;
using Azure.Messaging.EventGrid;
using Azure;
using System.Collections.Generic;

namespace ConfirmBookingAsync.Function
{
    public class ConfirmBookingAsyncFunction
    {
        private readonly ILogger<ConfirmBookingAsyncFunction> _logger;
        private readonly CosmosDbService _cosmosDbService;
        private readonly EventGridPublisherClient _eventGridClient;
        private readonly string _eventGridTopic;

        public ConfirmBookingAsyncFunction(
            ILogger<ConfirmBookingAsyncFunction> logger,
            CosmosDbService cosmosDbService,
            EventGridPublisherClient eventGridClient)
        {
            _logger = logger;
            _cosmosDbService = cosmosDbService;
            _eventGridClient = eventGridClient;
            _eventGridTopic = Environment.GetEnvironmentVariable("EventGridTopic") ?? "";
        }

        [Function("ConfirmBookingAsync")]
        public async Task<IActionResult> ConfirmBooking(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "confirmbookingasync")] HttpRequest req, 
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("ConfirmBookingAsync function processing request");

            try
            {
                // Read and deserialize the request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken);
                var bookingRequest = JsonConvert.DeserializeObject<BookingConfirmationRequest>(requestBody);
                var bookingReq = bookingRequest?.Booking;
                
                if (bookingReq == null || string.IsNullOrEmpty(bookingReq.Id))
                {
                    return new BadRequestObjectResult(new BookingConfirmationResponse
                    {
                        Success = false,
                        Message = "BookingId is required"
                    });
                }
                
                if (bookingReq.Customer == null || string.IsNullOrEmpty(bookingReq.Customer.Id))
                {
                    return new BadRequestObjectResult(new BookingConfirmationResponse 
                    { 
                        Success = false, 
                        Message = "Valid customer information is required" 
                    });
                }
                
                // Get the reservation
                dynamic reservation = await _cosmosDbService.GetItemAsync<dynamic>("Reservations", bookingReq.Id);
                
                if (reservation == null)
                {
                    return new BadRequestObjectResult(new BookingConfirmationResponse 
                    { 
                        Success = false, 
                        Message = $"Reservation with ID {bookingReq.Id} not found" 
                    });
                }

                // Check if the customer exists or create a new one
                CustomerModel existingCustomer = null;
                if (!string.IsNullOrEmpty(bookingReq.Customer.Id))
                {
                    existingCustomer = await _cosmosDbService.GetItemAsync<CustomerModel>("Customers", bookingReq.Customer.Id);
                }

                CustomerModel customer;
                if (existingCustomer == null)
                {
                    // Create a new customer with a generated Id if one wasn't provided
                    customer = new CustomerModel
                    {
                        Id = string.IsNullOrEmpty(bookingReq.Customer.Id) 
                            ? Guid.NewGuid().ToString() 
                            : bookingReq.Customer.Id,
                        FullName = bookingReq.Customer.FullName ?? string.Empty,
                        Email = bookingReq.Customer.Email ?? string.Empty,
                        Phone = bookingReq.Customer.Phone,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    
                    await _cosmosDbService.CreateItemAsync("Customers", customer, customer.Id);
                }
                else
                {
                    // Use existing customer but update info if needed
                    existingCustomer.FullName = bookingReq.Customer.FullName ?? existingCustomer.FullName;
                    existingCustomer.Email = bookingReq.Customer.Email ?? existingCustomer.Email;
                    existingCustomer.Phone = bookingReq.Customer.Phone;
                    existingCustomer.UpdatedAt = DateTime.UtcNow;
                    
                    await _cosmosDbService.UpdateItemAsync("Customers", existingCustomer, existingCustomer.Id);
                    customer = existingCustomer;
                }

                // Create a booking from the reservation
                var booking = new BookingModel
                {
                    Id = Guid.NewGuid().ToString(),
                    CustomerId = customer.Id,
                    SubServiceId = reservation.subServiceId.ToString(),
                    Date = DateTime.Parse(reservation.date.ToString()),
                    StartTime = TimeSpan.Parse(reservation.startTime.ToString()),
                    EndTime = TimeSpan.Parse(reservation.endTime.ToString()),
                    BookingStatus = BookingStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Create the booking in Cosmos DB
                await _cosmosDbService.CreateItemAsync("Bookings", booking, booking.Id);

                // Delete the reservation (optional, could also mark it as 'confirmed')
                await _cosmosDbService.DeleteItemAsync("Reservations", bookingReq.Id);

                _logger.LogInformation($"Successfully confirmed booking {booking.Id} from reservation {bookingReq.Id}");
                
                // Publish event to EventGrid for BookingReservedEmail function
                await PublishBookingReservedEventAsync(booking, existingCustomer ?? customer);
                
                // Return the confirmed booking details
                return new OkObjectResult(new BookingConfirmationResponse
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
                _logger.LogError(ex, "Error processing ConfirmBookingAsync request");
                return new ObjectResult(new BookingConfirmationResponse 
                { 
                    Success = false, 
                    Message = "An error occurred while processing the booking confirmation" 
                })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }
        
        private async Task PublishBookingReservedEventAsync(BookingModel booking, CustomerModel customer)
        {
            try
            {
                // Create data for the event
                var eventData = new
                {
                    booking.Id,
                    booking.CustomerId,
                    CustomerName = customer.FullName,
                    CustomerEmail = customer.Email,
                    CustomerPhone = customer.Phone,
                    booking.SubServiceId,
                    booking.Date,
                    booking.StartTime,
                    booking.EndTime,
                    booking.BookingStatus,
                    EventType = "BookingReserved"
                };
                
                // Create and publish the event
                var eventGridEvent = new EventGridEvent(
                    subject: $"booking/{booking.Id}",
                    eventType: "Justloccit.Booking.Reserved",
                    dataVersion: "1.0",
                    data: BinaryData.FromObjectAsJson(eventData));
                
                await _eventGridClient.SendEventAsync(eventGridEvent);
                
                _logger.LogInformation($"Published BookingReserved event for booking {booking.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error publishing BookingReserved event for booking {booking.Id}");
                // Continue with function execution even if event publishing fails
            }
        }
    }
} 