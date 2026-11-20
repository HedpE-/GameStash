using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Steam
{
    internal class SteamAchievementPercentagesPayload
    {
        [JsonPropertyName("achievements")]
        public List<SteamGlobalAchievementPercentage>? Achievements { get; set; }
    }

}
