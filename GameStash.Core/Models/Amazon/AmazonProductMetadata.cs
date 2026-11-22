using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Amazon
{
    public class AmazonProductMetadata
    {
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("genres")]
        public List<string>? Genres { get; set; }

        [JsonPropertyName("backgroundUrl1")]
        public string? BackgroundUrl1 { get; set; }

        [JsonPropertyName("backgroundUrl2")]
        public string? BackgroundUrl2 { get; set; }

        [JsonPropertyName("logoUrl")]
        public string? LogoUrl { get; set; }

        [JsonPropertyName("developer")]
        public string? Developer { get; set; }

        [JsonPropertyName("publisher")]
        public string? Publisher { get; set; }

        [JsonPropertyName("releaseDate")]
        public string? ReleaseDate { get; set; }
    }
}
