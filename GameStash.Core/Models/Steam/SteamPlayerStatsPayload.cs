using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Steam
{
    internal class SteamPlayerStatsPayload
    {
        [JsonPropertyName("steamID")]
        public string? SteamId { get; set; }

        [JsonPropertyName("gameName")]
        public string? GameName { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("achievements")]
        public List<SteamRawPlayerAchievement>? Achievements { get; set; }
    }
}
