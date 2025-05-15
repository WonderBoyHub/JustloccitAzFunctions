using Newtonsoft.Json;
namespace ConfirmBookingAsync.Function.Models;
public class CustomerModel
{
    [JsonProperty(PropertyName = "id")]
    public required string Id { get; set; }
    [JsonProperty(PropertyName = "fullName")]
    public required string FullName { get; set; }
    [JsonProperty(PropertyName = "email")]
    public required string Email { get; set; }
    [JsonProperty(PropertyName = "phone")]
    public required int Phone { get; set; }
    [JsonProperty(PropertyName = "createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [JsonProperty(PropertyName = "updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    [JsonProperty(PropertyName = "bookings")]
    public ICollection<BookingModel> Bookings { get; set; } = new List<BookingModel>();
}