using Newtonsoft.Json;
using System;

namespace ConfirmBookingAsync.Function.Models;
public class BookingModel
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonProperty("subServiceId")]
    public string SubServiceId { get; set; } = string.Empty;

    [JsonProperty("date")]
    public DateTime Date { get; set; }

    [JsonProperty("startTime")]
    public TimeSpan StartTime { get; set; }

    [JsonProperty("endTime")]
    public TimeSpan EndTime { get; set; }

    [JsonProperty("bookingStatus")]
    public BookingStatus BookingStatus { get; set; }

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [JsonProperty("customerName")]
    public string CustomerName { get; set; } = string.Empty;

    [JsonProperty("customerEmail")]
    public string CustomerEmail { get; set; } = string.Empty;

    [JsonProperty("customerPhone")]
    public string CustomerPhone { get; set; } = string.Empty;

    [JsonProperty("serviceId")]
    public string ServiceId { get; set; } = string.Empty;

    [JsonProperty("serviceName")]
    public string ServiceName { get; set; } = string.Empty;

    [JsonProperty("subServiceName")]
    public string SubServiceName { get; set; } = string.Empty;

    [JsonProperty("notes")]
    public string Notes { get; set; } = string.Empty;

    [JsonProperty("lockExpiresAt")]
    public DateTime? LockExpiresAt { get; set; }
    // Navigation properties
    [JsonProperty("customer")]
    public virtual Customer? Customer { get; set; }
    [JsonProperty("service")]
    public virtual ServiceModel? Service { get; set; }
    [JsonProperty("subService")]
    public virtual SubServiceModel? SubService { get; set; }
    [JsonProperty("employee")]
    public virtual Employee? Employee { get; set; }
}

public enum BookingStatus
{
    Pending = 0,
    Locked = 1,
    Confirmed = 2,
    Cancelled = 3,
    Expired = 4
}