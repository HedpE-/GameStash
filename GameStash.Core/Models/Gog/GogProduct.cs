using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Gog
{
    public class GogProduct
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("image")]
        public string Image { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("slug")]
        public string Slug { get; set; } = string.Empty;

        [JsonPropertyName("isGame")]
        public bool IsGame { get; set; }

        [JsonPropertyName("isMovie")]
        public bool IsMovie { get; set; }

        [JsonPropertyName("worksOn")]
        public GogWorksOn? WorksOn { get; set; }

        [JsonPropertyName("releaseDate")]
        public GogReleaseDate? ReleaseDate { get; set; }
    }
}
