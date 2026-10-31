using GameStash.Web.GUI.DTO;
using GameStash.Web.GUI.Interfaces;
using IgdbSharp;
using IgdbSharp.Enumerators;
using IgdbSharp.Models;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Services
{
    public class HomeDashboardService : IHomeDashboardService
    {
        private readonly ISettingsService _settingsService;

        // Central runtime memory cache blocks targeting local thread mutations
        private static HomeDashboardResponseDto? _cachedDashboard;
        private static readonly object _cacheLock = new();

        public HomeDashboardService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public async Task<HomeDashboardResponseDto> GetDashboardDataAsync()
        {
            lock (_cacheLock)
            {
                // Validate if cache layers match current universal calendar dates to reuse rows safely
                if (_cachedDashboard != null && _cachedDashboard.LastUpdated.HasValue &&
                    DateTime.UtcNow.Date == _cachedDashboard.LastUpdated.Value.Date)
                {
                    Log.ForContext<HomeDashboardService>().Debug("Returning cached IGDB dashboard data. Valid for the calendar day: {Day:yyyy-MM-dd}",
                        _cachedDashboard.LastUpdated.Value.ToLocalTime());
                    return _cachedDashboard;
                }
            }

            Log.ForContext<HomeDashboardService>().Information("New calendar day detected or cache empty. Launching fresh parallel queries to IGDB...");

            var igdbClient = _settingsService.GetIgdbClient();
            var freshDashboard = new HomeDashboardResponseDto();

            // Execute the exact 3 queries in parallel using maximum pipeline efficiency
            await Task.WhenAll(
                Task.Run(async () =>
                {
                    try
                    {
                        var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        var query = $"fields {GameCardDto.GameCardReleaseDateQueryFields}; where first_release_date != null & platforms = (6) & first_release_date <= {currentTimestamp}; sort first_release_date desc; limit 10;";
                        var rawGames = await igdbClient.QueryAsync<Game>("games", query: query, requestPriority: RequestPriority.High);
                        freshDashboard.LatestReleases = [.. rawGames.Select(GameCardDto.FromModel)];
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext<HomeDashboardService>().Error(ex, "Failed to fetch latest releases from IGDB.");
                    }
                }),
                Task.Run(async () =>
                {
                    try
                    {
                        var query = $"fields {GameCardDto.GameCardRatingQueryFields}; where rating != null & platforms != null & platforms = (6); sort rating desc; limit 10;";
                        var rawGames = await igdbClient.QueryAsync<Game>("games", query: query, requestPriority: RequestPriority.High);
                        freshDashboard.Top10RatedGames = [.. rawGames.Select(GameCardDto.FromModel)];
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext<HomeDashboardService>().Error(ex, "Failed to fetch top rated games from IGDB.");
                    }
                }),
                Task.Run(async () =>
                {
                    try
                    {
                        long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        var query = $"fields {GameCardDto.GameCardReleaseDateQueryFields}; where first_release_date > {currentTimestamp} & hypes > 0 & platforms != null & platforms = (6); sort hypes desc; limit 10;";
                        var rawGames = await igdbClient.QueryAsync<Game>("games", query: query, requestPriority: RequestPriority.High);
                        freshDashboard.MostAnticipated = [.. rawGames.Select(GameCardDto.FromModel)];
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext<HomeDashboardService>().Error(ex, "Failed to fetch most anticipated games from IGDB.");
                    }
                })
            );

            // Commit atomic properties using structural isolation updates
            lock (_cacheLock)
            {
                freshDashboard.LastUpdated = DateTime.UtcNow;
                _cachedDashboard = freshDashboard;
            }

            return freshDashboard;
        }
    }
}
