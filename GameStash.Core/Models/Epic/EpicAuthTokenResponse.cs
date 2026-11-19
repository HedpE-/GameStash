using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GameStash.Core.Models.Epic
{
    // Root myDeserializedClass = JsonSerializer.Deserialize<Root>(myJsonResponse);
    public class EpicAuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; set; }

        [JsonPropertyName("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonPropertyName("refresh_expires")]
        public int? RefreshExpires { get; set; }

        [JsonPropertyName("refresh_expires_at")]
        public DateTime? RefreshExpiresAt { get; set; }

        [JsonPropertyName("account_id")]
        public string AccountId { get; set; }

        [JsonPropertyName("client_id")]
        public string ClientId { get; set; }

        [JsonPropertyName("internal_client")]
        public bool? InternalClient { get; set; }

        [JsonPropertyName("client_service")]
        public string ClientService { get; set; }

        [JsonPropertyName("scope")]
        public List<object> Scope { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("app")]
        public string App { get; set; }

        [JsonPropertyName("in_app_id")]
        public string InAppId { get; set; }

        [JsonPropertyName("product_id")]
        public string ProductId { get; set; }

        [JsonPropertyName("application_id")]
        public string ApplicationId { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }

        [JsonPropertyName("acr")]
        public string Acr { get; set; }

        [JsonPropertyName("auth_time")]
        public DateTime? AuthTime { get; set; }
    }


}
