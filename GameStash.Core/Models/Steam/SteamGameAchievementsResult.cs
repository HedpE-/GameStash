using System;
using System.Collections.Generic;
using System.Text.Json;

namespace GameStash.Core.Models.Steam
{
    public class SteamGameAchievementsResult
    {
        public long AppId { get; set; }
        public string GameName { get; set; } = string.Empty;
        public int TotalAchievements { get; set; }
        public int UnlockedAchievements { get; set; }
        public List<SteamPlayerAchievement> Achievements { get; set; } = new();
    }
}
