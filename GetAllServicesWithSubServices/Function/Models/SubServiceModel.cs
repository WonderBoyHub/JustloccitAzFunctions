using System.Text.Json.Serialization;

namespace GetAllServicesWithSubServices.Function.Models
{
    public class SubServiceModel
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("serviceId")]
        public string ServiceId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("duration")]
        public int Duration { get; set; } = 30; // Duration in minutes

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("isAvailable")]
        public bool IsAvailable { get; set; } = true;
    }
} 