using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Steam
{
    public class SteamOwnedGamesData
    {
        [JsonPropertyName("game_count")]
        public int GameCount { get; set; }

        [JsonPropertyName("games")]
        public List<SteamRawGame> Games { get; set; } = new();
    }
}