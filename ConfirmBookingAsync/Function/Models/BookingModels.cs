using System;

namespace ConfirmBookingAsync.Function.Models
{
    public class BookingConfirmationRequest
    {
        public string BookingId { get; set; } = string.Empty;
        public CustomerDto Customer { get; set; } = new();
    }

    public class CustomerDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
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