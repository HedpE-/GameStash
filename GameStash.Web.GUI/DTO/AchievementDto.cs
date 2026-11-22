using System;

namespace GameStash.Web.GUI.DTO
{
    public class AchievementDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
        public string LockedIconUrl { get; set; } = string.Empty;
        public bool IsUnlocked { get; set; }
        public DateTimeOffset? UnlockedAt { get; set; }
        public double RarityPercent { get; set; }
        public double? Progress { get; set; }
        public int? Xp { get; set; }
        public bool IsHidden { get; set; }
    }
}
