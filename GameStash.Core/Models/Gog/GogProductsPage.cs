using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Gog
{
    public class GogProductsPage
    {
        [JsonPropertyName("pages")]
        public int Pages { get; set; }

        [JsonPropertyName("totalProducts")]
        public int TotalProducts { get; set; }

        [JsonPropertyName("products")]
        public List<GogProduct> Products { get; set; } = new();
    }
}