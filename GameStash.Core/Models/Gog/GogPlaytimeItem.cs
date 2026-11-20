using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Gog
{
    public class GogPlaytimeItem
    {
        [JsonPropertyName("game_id")]
        public string GameId { get; set; } = string.Empty;

        [JsonPropertyName("minutes_sum")]
        public int MinutesSum { get; set; }
    }
}
