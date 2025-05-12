using System;

namespace UpdateBooking.Function.Models
{
    public class UpdateBookingRequest
    {
        public string BookingId { get; set; } = string.Empty;
        public string? CustomerId { get; set; }
        public string? SubServiceId { get; set; }
        public DateTime? Date { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public BookingStatus? BookingStatus { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateBookingResponse
    {
        public bool Success { get; set; }
        public string? BookingId { get; set; }
        public string? CustomerId { get; set; }
        public DateTime? Date { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public BookingStatus? BookingStatus { get; set; }
        public string? Message { get; set; }
    }
} 