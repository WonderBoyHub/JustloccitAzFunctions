using System;

namespace DeleteBooking.Function.Models
{
    public class DeleteBookingRequest
    {
        public string BookingId { get; set; } = string.Empty;
    }

    public class DeleteBookingResponse
    {
        public bool Success { get; set; }
        public string? BookingId { get; set; }
        public string? Message { get; set; }
    }
} 