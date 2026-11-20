using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Gog
{
    public class GogPlaytimeResponse
    {
        [JsonPropertyName("items")]
        public List<GogPlaytimeItem> Items { get; set; } = new();
    }
}
