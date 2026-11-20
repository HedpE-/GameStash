using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Gog
{
    public class GogProductsPage
    {
        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("totalPages")]
        public int TotalPages { get; set; }

        [JsonPropertyName("totalProducts")]
        public int TotalProducts { get; set; }

        [JsonPropertyName("productsPerPage")]
        public int ProductsPerPage { get; set; }

        [JsonPropertyName("products")]
        public List<GogProduct> Products { get; set; } = new();
    }
}
