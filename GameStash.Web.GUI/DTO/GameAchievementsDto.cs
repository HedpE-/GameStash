using System.Collections.Generic;

namespace GameStash.Web.GUI.DTO
{
    public class GameAchievementsDto
    {
        public string Store { get; set; } = string.Empty;
        public int TotalAchievements { get; set; }
        public int UnlockedAchievements { get; set; }
        public int? TotalXp { get; set; }
        public int? EarnedXp { get; set; }
        public List<AchievementDto> Achievements { get; set; } = new();
    }
}
