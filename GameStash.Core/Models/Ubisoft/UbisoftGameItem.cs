using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Ubisoft
{
    public class UbisoftGameItem
    {
        [JsonPropertyName("spaceId")]
        public string SpaceId { get; set; } = string.Empty;

        [JsonPropertyName("applicationId")]
        public string ApplicationId { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("platform")]
        public string Platform { get; set; } = string.Empty;

        [JsonPropertyName("ownership")]
        public string Ownership { get; set; } = string.Empty;

        [JsonPropertyName("backgroundUrl")]
        public string? BackgroundUrl { get; set; }

        [JsonPropertyName("boxartUrl")]
        public string? BoxartUrl { get; set; }

        [JsonPropertyName("logoUrl")]
        public string? LogoUrl { get; set; }

        [JsonPropertyName("playtimeSeconds")]
        public long PlaytimeSeconds { get; set; }
    }
}
