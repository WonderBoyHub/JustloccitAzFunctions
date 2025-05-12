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
                
                if (bookingRequest == null || string.IsNullOrEmpty(bookingRequest.BookingId))
                {
                    return new BadRequestObjectResult(new BookingConfirmationResponse 
                    { 
                        Success = false, 
                        Message = "BookingId is required" 
                    });
                }
                
                if (bookingRequest.Customer == null || string.IsNullOrEmpty(bookingRequest.Customer.Id))
                {
                    return new BadRequestObjectResult(new BookingConfirmationResponse 
                    { 
                        Success = false, 
                        Message = "Valid customer information is required" 
                    });
                }
                
                // Get the reservation
                dynamic reservation = await _cosmosDbService.GetItemAsync<dynamic>("Reservations", bookingRequest.BookingId);
                
                if (reservation == null)
                {
                    return new BadRequestObjectResult(new BookingConfirmationResponse 
                    { 
                        Success = false, 
                        Message = $"Reservation with ID {bookingRequest.BookingId} not found" 
                    });
                }

                // Check if the customer exists or create a new one
                Customer existingCustomer = null;
                if (!string.IsNullOrEmpty(bookingRequest.Customer.Id))
                {
                    existingCustomer = await _cosmosDbService.GetItemAsync<Customer>("Customers", bookingRequest.Customer.Id);
                }

                Customer customer;
                if (existingCustomer == null)
                {
                    // Create a new customer with a generated Id if one wasn't provided
                    customer = new Customer
                    {
                        Id = string.IsNullOrEmpty(bookingRequest.Customer.Id) 
                            ? Guid.NewGuid().ToString() 
                            : bookingRequest.Customer.Id,
                        Name = bookingRequest.Customer.Name ?? string.Empty,
                        Email = bookingRequest.Customer.Email ?? string.Empty,
                        Phone = bookingRequest.Customer.Phone ?? string.Empty,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    
                    await _cosmosDbService.CreateItemAsync("Customers", customer, customer.Id);
                }
                else
                {
                    // Use existing customer but update info if needed
                    existingCustomer.Name = bookingRequest.Customer.Name ?? existingCustomer.Name;
                    existingCustomer.Email = bookingRequest.Customer.Email ?? existingCustomer.Email;
                    existingCustomer.Phone = bookingRequest.Customer.Phone ?? existingCustomer.Phone;
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
                await _cosmosDbService.DeleteItemAsync("Reservations", bookingRequest.BookingId);

                _logger.LogInformation($"Successfully confirmed booking {booking.Id} from reservation {bookingRequest.BookingId}");
                
                // Publish event to EventGrid for BookingReservedEmail function
                await PublishBookingReservedEventAsync(booking, customer);
                
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
        
        private async Task PublishBookingReservedEventAsync(BookingModel booking, Customer customer)
        {
            try
            {
                // Create data for the event
                var eventData = new
                {
                    booking.Id,
                    booking.CustomerId,
                    CustomerName = customer.Name,
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