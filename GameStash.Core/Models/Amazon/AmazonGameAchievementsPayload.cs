using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Amazon
{
    internal class AmazonGameAchievementsPayload
    {
        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }

        [JsonPropertyName("unlockedCount")]
        public int UnlockedCount { get; set; }

        [JsonPropertyName("items")]
        public List<AmazonRawAchievementItem> Items { get; set; } = new();
    }
}
