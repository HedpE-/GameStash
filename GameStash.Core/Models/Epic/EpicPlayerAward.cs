using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Epic
{
    internal class EpicPlayerAward
    {
        [JsonPropertyName("awardType")]
        public string? AwardType { get; set; }

        [JsonPropertyName("unlocked")]
        public bool Unlocked { get; set; }

        [JsonPropertyName("unlockDate")]
        public DateTimeOffset? UnlockDate { get; set; }
    }
}
