using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace ConfirmBookingAsync.Function.Models
{
    public class Customer
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [JsonProperty("fullName")]
        public string Name { get; set; }
        
        [JsonProperty("email")]
        public string Email { get; set; }
        
        [JsonProperty("phone")]
        public string Phone { get; set; }
        
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
} 