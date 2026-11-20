using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Steam
{
    // ---------------------------------------------------------
    // INTERNAL STEAM WEB API WRAPPERS
    // ---------------------------------------------------------

    internal class SteamPlayerAchievementsResponse
    {
        [JsonPropertyName("playerstats")]
        public SteamPlayerStatsPayload? PlayerStats { get; set; }
    }
}
