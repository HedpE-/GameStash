using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Epic
{
    internal class EpicPlayerAchievementNode
    {
        [JsonPropertyName("playerAchievement")]
        public EpicPlayerAchievementDetail? Detail { get; set; }
    }
}
