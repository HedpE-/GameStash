using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Epic
{
    public class EpicReleaseInfo
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("appId")]
        public string? AppId { get; set; }

        [JsonPropertyName("platform")]
        public List<string>? Platform { get; set; }
    }
}
