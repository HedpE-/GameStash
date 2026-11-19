using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Epic
{
    internal class EpicPlayerAchievementDetail
    {
        [JsonPropertyName("achievementName")]
        public string? AchievementName { get; set; }

        [JsonPropertyName("unlocked")]
        public bool Unlocked { get; set; }

        [JsonPropertyName("unlockDate")]
        public DateTimeOffset? UnlockDate { get; set; }

        [JsonPropertyName("progress")]
        public double Progress { get; set; }

        [JsonPropertyName("XP")]
        public int Xp { get; set; }

        [JsonPropertyName("rarity")]
        public EpicAchievementRarity? Rarity { get; set; }

        [JsonPropertyName("achievement")]
        public EpicAchievementMetadata? Metadata { get; set; }
    }
}
