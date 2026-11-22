namespace GameStash.Core.Models.Steam
{
    public class SteamStoreAppDetails
    {
        public long AppId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public string DetailedDescription { get; set; } = string.Empty;
        public string HeaderImage { get; set; } = string.Empty;
        public string CapsuleCoverUrl => $"https://store.steamstatic.com" + $"/store_item_assets/steam/apps/{AppId}/library_600x900.jpg";
        public string ReleaseDateText { get; set; } = string.Empty;
        public List<string> Developers { get; set; } = new();
        public List<string> Publishers { get; set; } = new();
        public List<string> Genres { get; set; } = new();
        public List<string> Platforms { get; set; } = new();
        public List<string> Screenshots { get; set; } = new();
    }

}
