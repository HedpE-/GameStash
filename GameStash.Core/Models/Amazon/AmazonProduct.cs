using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Amazon
{
    public class AmazonProduct
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("productLine")]
        public string ProductLine { get; set; } = string.Empty;

        [JsonPropertyName("sku")]
        public string Sku { get; set; } = string.Empty;

        [JsonPropertyName("productDetail")]
        public AmazonProductDetail? ProductDetail { get; set; }
    }
}
