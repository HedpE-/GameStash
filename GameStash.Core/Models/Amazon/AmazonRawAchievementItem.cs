using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Amazon
{
    internal class AmazonRawAchievementItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("unlocked")]
        public bool Unlocked { get; set; }

        [JsonPropertyName("unlockedAt")]
        public DateTimeOffset? UnlockedAt { get; set; }

        [JsonPropertyName("rarityPercentage")]
        public double RarityPercentage { get; set; }

        [JsonPropertyName("iconUrl")]
        public string? IconUrl { get; set; }

        [JsonPropertyName("lockedIconUrl")]
        public string? LockedIconUrl { get; set; }

        [JsonPropertyName("hidden")]
        public bool Hidden { get; set; }
    }
}
