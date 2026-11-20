using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Gog
{
    public class GogReleaseDate
    {
        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("timezone")]
        public string? Timezone { get; set; }
    }
}
