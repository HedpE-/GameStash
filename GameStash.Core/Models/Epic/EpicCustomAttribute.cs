using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Epic
{
    public class EpicCustomAttribute
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("value")]
        public string? Value { get; set; }
    }
}
