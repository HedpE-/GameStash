using GameStash.Core.Models;
using GameStash.Web.GUI.DTO;
using GameStash.Web.GUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Services
{
    public class UnifiedStashService(
        ILocalStashService localStashService,
        ISteamStashService steamStashService,
        IEpicStashService epicStashService,
        IGogStashService gogStashService,
        IAmazonStashService amazonStashService) : IUnifiedStashService
    {
        private readonly ILocalStashService _localStashService = localStashService;
        private readonly ISteamStashService _steamStashService = steamStashService;
        private readonly IEpicStashService _epicStashService = epicStashService;
        private readonly IGogStashService _gogStashService = gogStashService;
        private readonly IAmazonStashService _amazonStashService = amazonStashService;

        public async Task<List<UnifiedStashCardDto>> GetAllUnifiedGamesAsync(bool forceRefresh = false)
        {
            var localTask = SafeGetStashAsync(() => _localStashService.GetStashAsync(forceRefresh));
            var steamTask = SafeGetStashAsync(() => _steamStashService.GetStashAsync(forceRefresh));
            var epicTask = SafeGetStashAsync(() => _epicStashService.GetStashAsync(forceRefresh));
            var gogTask = SafeGetStashAsync(() => _gogStashService.GetStashAsync(forceRefresh));
            var amazonTask = SafeGetStashAsync(() => _amazonStashService.GetStashAsync(forceRefresh));

            await Task.WhenAll(localTask, steamTask, epicTask, gogTask, amazonTask);

            var allUnifiedGames = new List<UnifiedStashCardDto>();

            AppendSourceGames(allUnifiedGames, await localTask, "Local");
            AppendSourceGames(allUnifiedGames, await steamTask, "Steam");
            AppendSourceGames(allUnifiedGames, await epicTask, "Epic");
            AppendSourceGames(allUnifiedGames, await gogTask, "GOG");
            AppendSourceGames(allUnifiedGames, await amazonTask, "Amazon");

            return allUnifiedGames
                .OrderBy(g => g.SortName ?? g.Name)
                .ToList();
        }

        private static async Task<IReadOnlyList<T>> SafeGetStashAsync<T>(Func<Task<IReadOnlyList<T>>> getStashFunc) where T : StashGame
        {
            try
            {
                return await getStashFunc() ?? Array.Empty<T>();
            }
            catch
            {
                return Array.Empty<T>();
            }
        }

        private static void AppendSourceGames<T>(
            List<UnifiedStashCardDto> targetList,
            IEnumerable<T> sourceGames,
            string sourceLabel) where T : StashGame
        {
            foreach (var game in sourceGames)
            {
                if (game == null || string.IsNullOrWhiteSpace(game.Name))
                    continue;

                targetList.Add(new UnifiedStashCardDto(game, sourceLabel));
            }
        }
    }
}
