namespace GameStash.Core.Models.Gog
{
    public class GogPlayerAchievement
    {
        public string Id { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Unlocked { get; set; }
        public DateTimeOffset? UnlockDate { get; set; }
        public double RarityPercent { get; set; }
        public string? UnlockedIconUrl { get; set; }
        public string? LockedIconUrl { get; set; }
        public string? DisplayIconUrl => Unlocked ? UnlockedIconUrl : (LockedIconUrl ?? UnlockedIconUrl);
        public bool IsHidden { get; set; }
    }
}
