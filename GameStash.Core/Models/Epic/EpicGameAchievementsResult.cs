namespace GameStash.Core.Models.Epic
{
    /// <summary>
    /// Clean representation of a player's Epic Games achievements for a specific game.
    /// </summary>
    public class EpicGameAchievementsResult
    {
        public string SandboxId { get; set; } = string.Empty;
        public int TotalAchievements { get; set; }
        public int UnlockedAchievements { get; set; }
        public int TotalXp { get; set; }
        public int EarnedXp { get; set; }
        public List<EpicPlayerAchievement> Achievements { get; set; } = [];
    }
}
