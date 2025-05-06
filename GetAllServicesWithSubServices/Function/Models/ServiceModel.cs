using System.Text.Json.Serialization;

namespace GetAllServicesWithSubServices.Function.Models
{
    public class ServiceModel
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("isActive")]
        public string IsActive { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        [JsonPropertyName("duration")]
        public int Duration { get; set; } = 30; // Duration in minutes

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("isAvailable")]
        public bool IsAvailable { get; set; } = true;

        [JsonPropertyName("subServices")]
        public ICollection<SubServiceModel> SubServices { get; set; } = new List<SubServiceModel>();
    }
} 