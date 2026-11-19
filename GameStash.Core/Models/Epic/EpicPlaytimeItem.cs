using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Epic
{
    public class EpicPlaytimeItem
    {
        [JsonPropertyName("artifactId")]
        public string? ArtifactId { get; set; }

        [JsonPropertyName("totalTime")]
        public long TotalTimeSeconds { get; set; }
    }
}
