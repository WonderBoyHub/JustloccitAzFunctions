using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Justloccit.Function.Models
{
    public class Reservation
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [JsonPropertyName("subServiceId")]
        public string SubServiceId { get; set; }
        
        [JsonPropertyName("subServices")]
        public List<SubServiceReservation> SubServices { get; set; }
        
        [JsonPropertyName("date")]
        public string Date { get; set; }
        
        [JsonPropertyName("startTime")]
        public string StartTime { get; set; }
        
        [JsonPropertyName("endTime")]
        public string EndTime { get; set; }
        
        [JsonPropertyName("duration")]
        public int Duration { get; set; }
        
        [JsonPropertyName("bookingStatus ")]
        public ReservationStatus Status { get; set; } = ReservationStatus.Locked;
        
        [JsonPropertyName("lockExpiresAt")]
        public DateTime LockExpiresAt { get; set; }
        
        [JsonPropertyName("partitionKey")]
        public string PartitionKey { get; set; }
        
        [JsonPropertyName("serviceName")]
        public string ServiceName { get; set; }
    }

    public class SubServiceReservation
    {
        [JsonPropertyName("subServiceId")]
        public string SubServiceId { get; set; }
        
        [JsonPropertyName("duration")]
        public int Duration { get; set; }
        
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("id")]
        public string Id { get; set; }
    }

    public enum ReservationStatus
    {
        Pending = 0,
        Locked = 1,
        Confirmed = 2,
        Cancelled = 3,
        Expired = 4
    }
} 