using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Epic
{
    internal class EpicAchievementMetadata
    {
        [JsonPropertyName("unlockedDisplayName")]
        public string? UnlockedDisplayName { get; set; }

        [JsonPropertyName("lockedDisplayName")]
        public string? LockedDisplayName { get; set; }

        [JsonPropertyName("unlockedDescription")]
        public string? UnlockedDescription { get; set; }

        [JsonPropertyName("lockedDescription")]
        public string? LockedDescription { get; set; }

        [JsonPropertyName("unlockedIconLink")]
        public string? UnlockedIconLink { get; set; }

        [JsonPropertyName("lockedIconLink")]
        public string? LockedIconLink { get; set; }

        [JsonPropertyName("hidden")]
        public bool Hidden { get; set; }
    }
}
