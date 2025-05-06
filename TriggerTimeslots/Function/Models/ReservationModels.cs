using System;
using System.Text.Json.Serialization;

namespace Justloccit.Models
{
    public enum ReservationStatus
    {
        Pending = 0,
        Locked = 1,
        Confirmed = 2,
        Cancelled = 3,
        Expired = 4
    }

    public record ReservationDocument
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;
        
        public string SubServiceName { get; init; } = string.Empty;
        
        public string Date { get; init; } = string.Empty;
        
        public string StartTime { get; init; } = string.Empty;
        
        public string EndTime { get; init; } = string.Empty;
        
        public ReservationStatus Status { get; set; }
        
        public string SubServiceId { get; init; } = string.Empty;
        
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        
        public DateTime? LockExpiresAt { get; set; }
        
        public string Notes { get; set; } = string.Empty;
        
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