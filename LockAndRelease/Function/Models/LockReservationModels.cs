using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Justloccit.Function.Models
{
    public class LockSingleServiceRequest
    {
        [JsonPropertyName("subServiceId")]
        public string SubServiceId { get; set; }
        
        [JsonPropertyName("date")]
        public string Date { get; set; }
        
        [JsonPropertyName("startTime")]
        public string StartTime { get; set; }
    }
    
    public class LockMultipleServicesRequest
    {
        [JsonPropertyName("date")]
        public string Date { get; set; }
        
        [JsonPropertyName("startTime")]
        public string StartTime { get; set; }
        
        [JsonPropertyName("subServices")]
        public List<SubServiceRequest> SubServices { get; set; }
    }
    
    public class SubServiceRequest
    {
        [JsonPropertyName("subServiceId")]
        public string SubServiceId { get; set; }
        
        [JsonPropertyName("duration")]
        public int Duration { get; set; }
    }
    
    public class LockSingleServiceResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        
        [JsonPropertyName("bookingId")]
        public string BookingId { get; set; }
        
        [JsonPropertyName("lockExpiresAt")]
        public DateTime LockExpiresAt { get; set; }
        
        [JsonPropertyName("startTime")]
        public string StartTime { get; set; }
        
        [JsonPropertyName("endTime")]
        public string EndTime { get; set; }
        
        [JsonPropertyName("duration")]
        public int Duration { get; set; }
        
        [JsonPropertyName("serviceName")]
        public string ServiceName { get; set; }
    }
    
    public class LockMultipleServicesResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        
        [JsonPropertyName("bookingId")]
        public string BookingId { get; set; }
        
        [JsonPropertyName("lockExpiresAt")]
        public DateTime LockExpiresAt { get; set; }
        
        [JsonPropertyName("startTime")]
        public string StartTime { get; set; }
        
        [JsonPropertyName("endTime")]
        public string EndTime { get; set; }
        
        [JsonPropertyName("duration")]
        public int Duration { get; set; }
        
        [JsonPropertyName("subServices")]
        public List<SubServiceResponse> SubServices { get; set; }
    }
    
    public class SubServiceResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("duration")]
        public int Duration { get; set; }
    }
} 