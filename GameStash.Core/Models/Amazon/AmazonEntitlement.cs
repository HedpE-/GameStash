using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Amazon
{
    public class AmazonEntitlement
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("product")]
        public AmazonProduct? Product { get; set; }
    }
}
