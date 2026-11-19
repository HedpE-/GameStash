using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Epic
{
    public class EpicVerifyTokenResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("session_id")]
        public string SessionId { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }

        [JsonPropertyName("client_id")]
        public string ClientId { get; set; }

        [JsonPropertyName("internal_client")]
        public bool? InternalClient { get; set; }

        [JsonPropertyName("client_service")]
        public string ClientService { get; set; }

        [JsonPropertyName("account_id")]
        public string AccountId { get; set; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; set; }

        [JsonPropertyName("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        [JsonPropertyName("auth_method")]
        public string AuthMethod { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("app")]
        public string App { get; set; }

        [JsonPropertyName("in_app_id")]
        public string InAppId { get; set; }

        [JsonPropertyName("scope")]
        public List<object> Scope { get; set; }

        [JsonPropertyName("product_id")]
        public string ProductId { get; set; }

        [JsonPropertyName("application_id")]
        public string ApplicationId { get; set; }

        [JsonPropertyName("acr")]
        public string Acr { get; set; }

        [JsonPropertyName("auth_time")]
        public DateTime? AuthTime { get; set; }
    }
}
