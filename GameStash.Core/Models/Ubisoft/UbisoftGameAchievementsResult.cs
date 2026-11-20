namespace GameStash.Core.Models.Ubisoft
{
    // ---------------------------------------------------------
    // ACHIEVEMENT / CHALLENGE MODELS
    // ---------------------------------------------------------

    public class UbisoftGameAchievementsResult
    {
        public string SpaceId { get; set; } = string.Empty;
        public int TotalAchievements { get; set; }
        public int UnlockedAchievements { get; set; }
        public int TotalXp { get; set; }
        public int EarnedXp { get; set; }
        public List<UbisoftPlayerAchievement> Achievements { get; set; } = new();
    }
}
