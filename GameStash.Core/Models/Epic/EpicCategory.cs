using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Epic
{
    public class EpicCategory
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;
    }
}
