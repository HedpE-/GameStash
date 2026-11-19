using GameStash.Core.Models;
using GameStash.Web.GUI.Interfaces;
using IgdbSharp.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Services
{
    public class GameDetailsService : IGameDetailsService
    {
        private readonly ISettingsService _settingsService;

        public GameDetailsService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public async Task<T?> GetGameDetailsAsync<T>(long gameId) where T : Game
        {
            try
            {
                var igdbClient = await _settingsService.GetIgdbClient();
                var query = $"fields {StashGame.FullQueryFields}; where id = {gameId};";

                var games = await igdbClient.QueryAsync<T>(IgdbSharp.Endpoints.Games, query: query);

                return games?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Log.ForContext<GameDetailsService>().Error(ex, "Error...");
                return null;
            }
        }

        public async Task<IEnumerable<T>> GetGamesAsync<T>(long[] gameIds) where T : Game
        {
            try
            {
                var igdbClient = await _settingsService.GetIgdbClient();
                var query = $"fields {StashGame.GameCardQueryFields},{StashGame.ReleaseDateQueryField},platforms.*;" +
                    $" where id = ({string.Join(",", gameIds)}) & game_type = 0;";

                var games = await igdbClient.QueryAsync<T>(IgdbSharp.Endpoints.Games, query: query, limitResults: gameIds.Length);

                return games
                    .OrderByDescending(g => g.FirstReleaseDate.HasValue)
                    .ThenByDescending(g => g.FirstReleaseDate?.Year)
                    .ThenBy(g => g.Name);
            }
            catch (Exception ex)
            {
                Log.ForContext<GameDetailsService>().Error(ex, "Error...");
                return null;
            }
        }

        public async Task<IEnumerable<Game>> GetSameFranchiseGames(long franchiseId)
        {
            try
            {
                var igdbClient = await _settingsService.GetIgdbClient();
                var query = $"fields {StashGame.FullQueryFields}; where franchise = {franchiseId};";

                var games = await igdbClient.QueryAsync<Game>(IgdbSharp.Endpoints.Games, query: query);

                return games;
            }
            catch (Exception ex)
            {
                Log.ForContext<GameDetailsService>().Error(ex, "Error...");
                return null;
            }
        }

        public async Task<T?> GetGameBySteamAppIdAsync<T>(long steamAppId) where T : Game
        {
            try
            {
                var igdbClient = await _settingsService.GetIgdbClient();

                // Searches IGDB by external_games reference: category 1 = Steam
                var query = $"fields game.*; where external_game_source = 1 & uid = \"{steamAppId}\";";

                var extResults = await igdbClient.QueryAsync<ExternalGame>(IgdbSharp.Endpoints.ExternalGames, query: query, limitResults: 1);

                var igdbGameId = extResults?.FirstOrDefault()?.Game?.Id
                      ?? extResults?.FirstOrDefault()?.Game?.Value?.Id;

                // 2. If the IGDB Game ID was found, fetch the full game metadata graph
                if (igdbGameId.HasValue && igdbGameId.Value > 0)
                {
                    return await GetGameDetailsAsync<T>(igdbGameId.Value);
                }

                Log.ForContext<GameDetailsService>().Warning("No matching IGDB entry found for Steam AppID: {AppId}", steamAppId);
                return null;
            }
            catch (Exception ex)
            {
                Log.ForContext<GameDetailsService>().Error(ex, "Failed to resolve game from IGDB by Steam AppID: {AppId}", steamAppId);
                return null;
            }
        }
    }
}