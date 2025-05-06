using Newtonsoft.Json;
namespace ConfirmBookingAsync.Function.Models;
public class Employee
{
    [JsonProperty(PropertyName = "id")]
    public string Id { get; set; }
    [JsonProperty(PropertyName = "firstName")]
    public string FirstName { get; set; }
    public string LastName { get; set; }
    [JsonProperty(PropertyName = "email")]
    public string Email { get; set; }
    public string Phone { get; set; }
    [JsonProperty(PropertyName = "isActive")]
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}