using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Steam
{
    internal class SteamGameSchemaResponse
    {
        [JsonPropertyName("game")]
        public SteamGameSchemaPayload? Game { get; set; }
    }
}
