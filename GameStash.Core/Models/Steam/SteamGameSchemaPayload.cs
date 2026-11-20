using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Steam
{
    internal class SteamGameSchemaPayload
    {
        [JsonPropertyName("gameName")]
        public string? GameName { get; set; }

        [JsonPropertyName("availableGameStats")]
        public SteamAvailableGameStats? AvailableGameStats { get; set; }
    }
}
