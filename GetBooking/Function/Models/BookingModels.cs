using System;

namespace GetBooking.Function.Models
{
    public class GetBookingRequest
    {
        public string BookingId { get; set; } = string.Empty;
    }

    public class GetBookingResponse
    {
        public bool Success { get; set; }
        public BookingDto? Booking { get; set; }
        public string? Message { get; set; }
    }

    public class BookingDto
    {
        public string Id { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string ServiceId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string SubServiceId { get; set; } = string.Empty;
        public string SubServiceName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public BookingStatus BookingStatus  { get; set; } 
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
} 