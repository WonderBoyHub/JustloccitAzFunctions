using System;

namespace CreateBooking.Function.Models
{
    public class CreateBookingRequest
    {
        public BookingModel Booking { get; set; } = new();
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string? Notes { get; set; }
        public string? CustomerId { get; set; }
        public string? SubServiceId { get; set; }
        public CustomerModel? CustomerInfo { get; set; }
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