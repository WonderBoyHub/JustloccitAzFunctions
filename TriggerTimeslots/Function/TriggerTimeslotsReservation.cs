using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Justloccit.Models;
using Justloccit.Services;

namespace Justloccit.Function
{
    public class TriggerTimeslotsReservation
    {
        private readonly ICosmosDbService _cosmosDbService;
        private readonly ILogger<TriggerTimeslotsReservation> _logger;

        public TriggerTimeslotsReservation(ICosmosDbService cosmosDbService, ILogger<TriggerTimeslotsReservation> logger)
        {
            _cosmosDbService = cosmosDbService;
            _logger = logger;
        }

        /// <summary>
        /// Triggered by changes in the ReservationsContainer
        /// Updates timeslots based on reservation changes
        /// </summary>
        [Function("TriggerTimeslotsReservation")]
        public async Task Run(
            [CosmosDBTrigger(
                databaseName: "CosmosDb:ReservationsDatabase",
                containerName: "CosmosDb:ReservationsContainer",
                Connection = "CosmosDb:ConnectionString",
                LeaseContainerName = "leases",
                CreateLeaseContainerIfNotExists = true)]
            IReadOnlyList<ReservationDocument> reservations)
        {
            if (reservations == null || reservations.Count == 0)
            {
                _logger.LogInformation("No reservation changes detected");
                return;
            }

            _logger.LogInformation("Processing {Count} reservation changes", reservations.Count);

            foreach (var reservation in reservations)
            {
                try
                {
                    await ProcessReservation(reservation);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing reservation with ID {Id}", reservation.Id);
                }
            }
        }

        private async Task ProcessReservation(ReservationDocument reservation)
        {
            _logger.LogInformation("Processing reservation {Id} for date {Date} with bookingStatus  {Status}", 
                reservation.Id, reservation.Date, reservation.Status);

            // Parse reservation time slots
            if (!DateTime.TryParse(reservation.StartTime, out DateTime startTime) ||
                !DateTime.TryParse(reservation.EndTime, out DateTime endTime))
            {
                _logger.LogError("Invalid time format in reservation {Id}", reservation.Id);
                return;
            }

            // Calculate start and end in minutes since midnight
            int startMinutes = startTime.Hour * 60 + startTime.Minute;
            int endMinutes = endTime.Hour * 60 + endTime.Minute;

            // Get the timeslot document for the reservation date
            var partitionKey = reservation.Date.Substring(0, 7); // YYYY-MM
            var timeslot = await _cosmosDbService.GetTimeslotAsync(reservation.Date, partitionKey);

            if (timeslot == null)
            {
                _logger.LogWarning("Timeslot document for date {Date} not found, cannot process reservation {Id}", 
                    reservation.Date, reservation.Id);
                return;
            }

            // Update the timeslots based on reservation bookingStatus 
            bool isAvailable = reservation.Status == ReservationStatus.Cancelled || 
                               reservation.Status == ReservationStatus.Expired;

            bool anyChanges = false;

            foreach (var slot in timeslot.TimeSlots)
            {
                if (slot.TotalMinutes >= startMinutes && slot.TotalMinutes < endMinutes)
                {
                    if (isAvailable)
                    {
                        // Only clear if this reservation was the one that booked it
                        if (slot.BookingId == reservation.Id)
                        {
                            slot.IsAvailable = true;
                            slot.BookingId = string.Empty;
                            slot.BookedBy = string.Empty;
                            slot.SubServiceId = string.Empty;
                            anyChanges = true;
                        }
                    }
                    else if (reservation.Status == ReservationStatus.Confirmed || 
                             reservation.Status == ReservationStatus.Locked)
                    {
                        // Only update if the slot is available or was previously booked by this reservation
                        if (slot.IsAvailable || slot.BookingId == reservation.Id)
                        {
                            slot.IsAvailable = false;
                            slot.BookingId = reservation.Id;
                            slot.BookedBy = reservation.SubServiceName;
                            slot.SubServiceId = reservation.SubServiceId;
                            anyChanges = true;
                        }
                        else
                        {
                            _logger.LogWarning("Timeslot at {Time} on {Date} is already booked by another reservation", 
                                slot.DisplayTime, reservation.Date);
                        }
                    }
                }
            }

            // Update timeslot document if changes were made
            if (anyChanges)
            {
                // Check overall availability (if any slot is available, the day is available)
                timeslot.IsAvailable = timeslot.TimeSlots.Any(ts => ts.IsAvailable);
                
                await _cosmosDbService.UpdateTimeslotAsync(timeslot);
                _logger.LogInformation("Updated timeslots for date {Date} based on reservation {Id}", 
                    reservation.Date, reservation.Id);
            }
            else
            {
                _logger.LogInformation("No changes needed for timeslots on date {Date}", reservation.Date);
            }
        }
    }
} 