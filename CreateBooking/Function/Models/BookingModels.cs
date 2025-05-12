using System;

namespace CreateBooking.Function.Models
{
    public class CreateBookingRequest
    {
        public string CustomerId { get; set; } = string.Empty;
        public string SubServiceId { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string? Notes { get; set; }
    }

    public class CreateBookingResponse
    {
        public bool Success { get; set; }
        public string? BookingId { get; set; }
        public string? CustomerId { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string? Message { get; set; }
    }
} 