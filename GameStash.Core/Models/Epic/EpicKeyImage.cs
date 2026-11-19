using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Epic
{
    public class EpicKeyImage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }
}
