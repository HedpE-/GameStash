using System.Reflection.Metadata;
using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Epic
{
    public class EpicCatalogItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("namespace")]
        public string Namespace { get; set; } = string.Empty;

        [JsonPropertyName("releaseInfo")]
        public List<EpicReleaseInfo>? ReleaseInfo { get; set; }

        // Helper to get the AppName directly from the Windows release info
        [JsonIgnore]
        public string? AppName => ReleaseInfo?
            .FirstOrDefault(r => r.Platform?.Contains("Windows", StringComparer.OrdinalIgnoreCase) == true)?.AppId
            ?? ReleaseInfo?.FirstOrDefault()?.AppId;

        [JsonPropertyName("productSlug")]
        public string? ProductSlug { get; set; }

        [JsonPropertyName("urlSlug")]
        public string? UrlSlug { get; set; }

        [JsonPropertyName("categories")]
        public List<EpicCategory>? Categories { get; set; }

        [JsonPropertyName("keyImages")]
        public List<EpicKeyImage>? KeyImages { get; set; }

        [JsonPropertyName("customAttributes")]
        public Dictionary<string, EpicCustomAttribute>? CustomAttributes { get; set; }

        public string? GetStoreSlug()
        {
            if (!string.IsNullOrWhiteSpace(ProductSlug))
                return ProductSlug;

            if (!string.IsNullOrWhiteSpace(UrlSlug))
                return UrlSlug;

            if (CustomAttributes != null &&
                CustomAttributes.TryGetValue("com.epicgames.app.productSlug", out var attr) &&
                !string.IsNullOrWhiteSpace(attr.Value))
            {
                return attr.Value;
            }

            return null;
        }
    }
}
