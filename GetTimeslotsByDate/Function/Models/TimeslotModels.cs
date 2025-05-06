using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Justloccit.Models
{
    public record TimeInfo
    {
        public int Hours { get; init; }
        public int Minutes { get; init; }
        public int TotalMinutes { get; init; }
        public string DisplayTime { get; init; } = string.Empty;
    }

    public record TimeSlot
    {
        public TimeInfo Time { get; init; } = new();
        public bool IsAvailable { get; set; } = true;
        public int Hours { get; init; }
        public int Minutes { get; init; }
        public int TotalMinutes { get; init; }
        public string DisplayTime { get; init; } = string.Empty;
        public string BookedBy { get; set; } = string.Empty;
        public string BookingId { get; set; } = string.Empty;
        public string SubServiceId { get; set; } = string.Empty;
    }

    public record TimeslotDocument
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;
        
        public string Date { get; init; } = string.Empty;
        
        public List<TimeSlot> TimeSlots { get; set; } = new();
        
        public bool IsAvailable { get; set; } = true;
        
        public string SpecialNotes { get; set; } = string.Empty;
        
        public string PartitionKey { get; init; } = string.Empty;

        // System properties are handled by the SDK
        [JsonPropertyName("_rid")]
        public string? ResourceId { get; set; }
        
        [JsonPropertyName("_etag")]
        public string? ETag { get; set; }
        
        [JsonPropertyName("_ts")]
        public long? Timestamp { get; set; }
        
        [JsonPropertyName("_self")]
        public string? SelfLink { get; set; }
    }
} 