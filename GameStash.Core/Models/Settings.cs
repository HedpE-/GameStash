namespace GameStash.Core.Models
{
    public class Settings
    {
        public string IGDB_Client_ID { get; set; }
        public string IGDB_Secret { get; set; }
        public string SteamId64 { get; set; }
        public string SteamApiKey { get; set; }
        public string EpicRefreshToken { get; set; }
        public string EpicAccessToken { get; set; }
        public string GogRefreshToken { get; set; }
        public string GogAccessToken { get; set; }
        public string AmazonRefreshToken { get; set; }
        public string AmazonAccessToken { get; set; }
    }
}