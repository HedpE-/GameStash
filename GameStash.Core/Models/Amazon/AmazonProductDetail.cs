using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Amazon
{
    public class AmazonProductDetail
    {
        [JsonPropertyName("details")]
        public AmazonProductMetadata? Details { get; set; }

        [JsonPropertyName("iconUrl")]
        public string? IconUrl { get; set; }
    }
}
