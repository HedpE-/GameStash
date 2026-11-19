using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Epic
{
    public class EpicLibraryResponse
    {
        [JsonPropertyName("records")]
        public List<EpicLibraryRecord> Records { get; set; } = new();

        [JsonPropertyName("responseMetadata")]
        public EpicResponseMetadata? ResponseMetadata { get; set; }
    }
}
