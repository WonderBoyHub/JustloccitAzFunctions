using Newtonsoft.Json;
using System;

namespace ConfirmBookingAsync.Function.Models
{
    public class SubServiceModel
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonProperty("serviceId")]
        public string ServiceId { get; set; } = string.Empty;
        
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;
        
        [JsonProperty("price")]
        public decimal Price { get; set; }
        
        [JsonProperty("durationMinutes")]
        public int DurationMinutes { get; set; }
        
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        [JsonProperty("isActive")]
        public bool IsActive { get; set; } = true;
    }
} 