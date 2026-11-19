using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Steam
{
    public class SteamRawGame
    {
        [JsonPropertyName("appid")]
        public long AppId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("playtime_forever")]
        public int PlaytimeForeverMinutes { get; set; }

        [JsonPropertyName("img_icon_url")]
        public string ImgIconUrl { get; set; } = string.Empty;

        [JsonPropertyName("rtime_last_played")]
        public long LastPlayedTimestamp { get; set; }
    }
}