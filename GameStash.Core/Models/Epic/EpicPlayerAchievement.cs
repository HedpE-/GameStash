namespace GameStash.Core.Models.Epic
{
    public class EpicPlayerAchievement
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Unlocked { get; set; }
        public DateTimeOffset? UnlockDate { get; set; }
        public double Progress { get; set; }
        public int Xp { get; set; }
        public double RarityPercent { get; set; }
        public string? UnlockedIconUrl { get; set; }
        public string? LockedIconUrl { get; set; }
        public string? DisplayIconUrl => Unlocked ? UnlockedIconUrl : (LockedIconUrl ?? UnlockedIconUrl);
        public bool IsHidden { get; set; }
    }
}
