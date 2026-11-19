using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Gog
{
    public class GogUserData
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("isLoggedIn")]
        public bool IsLoggedIn { get; set; }
    }
}