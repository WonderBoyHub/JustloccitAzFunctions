using System.Text.Json.Serialization;

namespace Justloccit.Function.Models
{
    public class ReleaseReservationRequest
    {
        [JsonPropertyName("bookingId")]
        public string BookingId { get; set; }
        
        [JsonPropertyName("date")]
        public string Date { get; set; }
    }
    
    public class ReleaseReservationResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
} 