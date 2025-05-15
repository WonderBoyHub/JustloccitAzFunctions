using System;

namespace ConfirmBookingAsync.Function.Models
{
    public class BookingConfirmationRequest
    {
        public BookingModel Booking { get; set; } = new();
    }


    public class BookingConfirmationResponse
    {
        public bool Success { get; set; }
        public string? BookingId { get; set; }
        public string? CustomerId { get; set; }
        public DateTime? Date { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public string? Message { get; set; }
    }
} 