using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Epic
{
    public class EpicLibraryRecord
    {
        [JsonPropertyName("namespace")]
        public string Namespace { get; set; } = string.Empty;

        [JsonPropertyName("catalogItemId")]
        public string CatalogItemId { get; set; } = string.Empty;

        [JsonPropertyName("appName")]
        public string AppName { get; set; } = string.Empty;

        [JsonPropertyName("productId")]
        public string ProductId { get; set; } = string.Empty;
    }
}
