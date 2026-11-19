using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameStash.Core.Models
{
    public class SteamStashGame : StashGame
    {
        private long? _extId;
        public long? ExtId
        {
            get => _extId;
            set => _extId = value;
        }

        private int _playtimeMinutes;
        public int PlaytimeMinutes
        {
            get => _playtimeMinutes;
            set => _playtimeMinutes = value;
        }
    }
}
