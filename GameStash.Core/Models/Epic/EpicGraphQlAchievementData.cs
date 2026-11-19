using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Epic
{
    internal class EpicGraphQlAchievementData
    {
        [JsonPropertyName("PlayerAchievement")]
        public EpicPlayerAchievementContainer? PlayerAchievement { get; set; }
    }
}
