using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Gog
{
    internal class GogAchievementItem
    {
        [JsonPropertyName("achievement_id")]
        public string AchievementId { get; set; } = string.Empty;

        [JsonPropertyName("achievement_key")]
        public string AchievementKey { get; set; } = string.Empty;

        [JsonPropertyName("visible")]
        public bool Visible { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("image_url_unlocked")]
        public string? ImageUrlUnlocked { get; set; }

        [JsonPropertyName("image_url_locked")]
        public string? ImageUrlLocked { get; set; }

        [JsonPropertyName("date_unlocked")]
        public DateTimeOffset? DateUnlocked { get; set; }

        [JsonPropertyName("rarity")]
        public double Rarity { get; set; }
    }
}
