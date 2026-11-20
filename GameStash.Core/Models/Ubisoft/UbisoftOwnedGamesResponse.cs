using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Ubisoft
{
    public class UbisoftOwnedGamesResponse
    {
        [JsonPropertyName("games")]
        public List<UbisoftGameItem> Games { get; set; } = new();
    }
}
