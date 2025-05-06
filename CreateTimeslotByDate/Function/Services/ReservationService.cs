using ConfirmBookingAsync.Function.Data;
using ConfirmBookingAsync.Function.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace ConfirmBookingAsync.Function.Services
{
    public class ReservationService
    {
        private readonly CosmosDbService _cosmosDbService;

        public ReservationService(CosmosDbService cosmosDbService)
        {
            _cosmosDbService = cosmosDbService;
        }

        public async Task<Booking> ConfirmBookingAsync(string reservationId, Customer customer, CancellationToken cancellationToken = default)
        {
            // Get the reservation
            dynamic reservation = await _cosmosDbService.GetItemAsync<dynamic>("ReservationsContainer", reservationId);
            
            if (reservation == null)
            {
                throw new InvalidOperationException($"Reservation with ID {reservationId} not found");
            }

            // Check if the customer exists or create a new one
            Customer existingCustomer = null;
            if (!string.IsNullOrEmpty(customer.Id))
            {
                existingCustomer = await _cosmosDbService.GetItemAsync<Customer>("CustomersContainer", customer.Id);
            }

            if (existingCustomer == null)
            {
                // Create a new customer with a generated Id if one wasn't provided
                if (string.IsNullOrEmpty(customer.Id))
                {
                    customer.Id = Guid.NewGuid().ToString();
                }
                
                await _cosmosDbService.CreateItemAsync("CustomersContainer", customer, customer.Id);
            }
            else
            {
                // Use existing customer but update info if needed
                existingCustomer.Name = customer.Name;
                existingCustomer.Email = customer.Email;
                existingCustomer.Phone = customer.Phone;
                await _cosmosDbService.UpdateItemAsync("CustomersContainer", existingCustomer, existingCustomer.Id);
                customer = existingCustomer;
            }

            // Create a booking from the reservation
            var booking = new Booking
            {
                Id = Guid.NewGuid().ToString(),
                CustomerId = customer.Id,
                SubServiceId = reservation.subServiceId.ToString(),
                Date = DateTime.Parse(reservation.date.ToString()),
                StartTime = TimeSpan.Parse(reservation.startTime.ToString()),
                EndTime = TimeSpan.Parse(reservation.endTime.ToString()),
                Status = "Confirmed",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Create the booking in Cosmos DB
            await _cosmosDbService.CreateItemAsync("BookingsContainer", booking, booking.Id);

            // Delete the reservation (optional, could also mark it as 'confirmed')
            await _cosmosDbService.DeleteItemAsync("ReservationsContainer", reservationId);

            return booking;
        }
    }
} 