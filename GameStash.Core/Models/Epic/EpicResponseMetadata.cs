using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Epic
{
    public class EpicResponseMetadata
    {
        [JsonPropertyName("nextCursor")]
        public string? NextCursor { get; set; }
    }
}
