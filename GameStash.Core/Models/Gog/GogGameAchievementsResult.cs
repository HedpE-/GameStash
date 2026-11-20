using System;
using System.Collections.Generic;

namespace GameStash.Core.Models.Gog
{
    public class GogGameAchievementsResult
    {
        public long GameId { get; set; }
        public int TotalAchievements { get; set; }
        public int UnlockedAchievements { get; set; }
        public List<GogPlayerAchievement> Achievements { get; set; } = new();
    }
}
