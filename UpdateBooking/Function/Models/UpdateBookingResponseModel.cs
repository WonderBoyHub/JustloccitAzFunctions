using System;

namespace UpdateBooking.Function.Models
{
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