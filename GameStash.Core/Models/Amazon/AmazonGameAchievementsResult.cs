namespace GameStash.Core.Models.Amazon
{
    public class AmazonGameAchievementsResult
    {
        public string ProductId { get; set; } = string.Empty;
        public int TotalAchievements { get; set; }
        public int UnlockedAchievements { get; set; }
        public List<AmazonPlayerAchievement> Achievements { get; set; } = new();
    }
}
