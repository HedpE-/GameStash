using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Steam
{
    public class SteamOwnedGamesResponse
    {
        [JsonPropertyName("response")]
        public SteamOwnedGamesData Response { get; set; } = new();
    }
}