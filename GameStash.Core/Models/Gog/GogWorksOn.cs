using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Gog
{
    public class GogWorksOn
    {
        [JsonPropertyName("Windows")]
        public bool Windows { get; set; }

        [JsonPropertyName("Mac")]
        public bool Mac { get; set; }

        [JsonPropertyName("Linux")]
        public bool Linux { get; set; }
    }
}
