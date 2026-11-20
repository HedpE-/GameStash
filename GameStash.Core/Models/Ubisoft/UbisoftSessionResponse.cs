using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Ubisoft
{
    public class UbisoftSessionResponse
    {
        [JsonPropertyName("ticket")]
        public string Ticket { get; set; } = string.Empty;

        [JsonPropertyName("rememberMeTicket")]
        public string? RememberMeTicket { get; set; }

        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("nameOnPlatform")]
        public string NameOnPlatform { get; set; } = string.Empty;

        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        [JsonPropertyName("expiration")]
        public DateTimeOffset? Expiration { get; set; }

        [JsonPropertyName("twoFactorAuthenticationTicket")]
        public string? TwoFactorAuthenticationTicket { get; set; }
    }
}
