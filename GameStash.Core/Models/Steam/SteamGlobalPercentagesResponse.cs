using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Steam
{
    internal class SteamGlobalPercentagesResponse
    {
        [JsonPropertyName("achievementpercentages")]
        public SteamAchievementPercentagesPayload? AchievementPercentages { get; set; }
    }
}
