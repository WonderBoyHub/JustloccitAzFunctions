using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace ConfirmBookingAsync.Function.Models
{
    public class ServiceModel
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;
        
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        [JsonProperty("isActive")]
        public bool IsActive { get; set; } = true;
    }
} 