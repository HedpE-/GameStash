using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Gog
{
    public class GogTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;
    }
}