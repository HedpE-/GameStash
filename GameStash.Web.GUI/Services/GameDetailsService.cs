using GameStash.Core.Models;
using GameStash.Web.GUI.Interfaces;
using Serilog;
using System;
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

        public async Task<T?> GetGameDetailsAsync<T>(long gameId) where T : IgdbSharp.Models.Game
        {
            try
            {
                var igdbClient = _settingsService.GetIgdbClient();
                var query = $"fields {Game.FullQueryFields}, videos.*; where id = {gameId};";

                // Agora o T é reconhecido aqui
                var games = await igdbClient.QueryAsync<T>("games", query: query);

                return games?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Log.ForContext<GameDetailsService>().Error(ex, "Error...");
                return null;
            }
        }
    }
}