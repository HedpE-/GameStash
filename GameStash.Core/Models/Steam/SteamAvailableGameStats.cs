using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Steam
{
    internal class SteamAvailableGameStats
    {
        [JsonPropertyName("achievements")]
        public List<SteamSchemaAchievement>? Achievements { get; set; }
    }
}
