using Common.Lib.Collections;
using Common.Lib.HelperClasses;
using IgdbSharp;
using IgdbSharp.Models;
using GameStash.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using IgdbSharp.Enumerators;

namespace GameStash.MVVM.GUI.ViewModels
{
    public class HomeViewModel : ObservableObject
    {
        private ObservableCollection<Game> _latestReleases;
        public ObservableCollection<Game> LatestReleases
        {
            get => _latestReleases;
            set => SetProperty(ref _latestReleases, value);
        }

        private ObservableCollection<Game> _top10RatedGames;
        public ObservableCollection<Game> Top10RatedGames
        {
            get => _top10RatedGames;
            set => SetProperty(ref _top10RatedGames, value);
        }

        private ObservableCollection<Game> _mostAnticipated;
        public ObservableCollection<Game> MostAnticipated
        {
            get => _mostAnticipated;
            set => SetProperty(ref _mostAnticipated, value);
        }

        public HomeViewModel()
        {
            PopulateQueries();
        }

        private async Task PopulateQueries()
        {
            var settings = await SqliteHelper.GetSettingsAsync();

            var igdbClient = new IgdbClient(settings.IGDB_Client_ID, settings.IGDB_Secret);

            await Task.WhenAll(
                Task.Run(async () =>
                {
                    string queryString = null;
                    try
                    {
                        var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        queryString = $"fields {Core.Models.Game.FullQueryFields}; where first_release_date != null & platforms = (6) & first_release_date <= {currentTimestamp}; sort first_release_date desc; limit 10;";
                        var IgdbMatches = await igdbClient.QueryAsync<Game>(Endpoints.Games, queryString, requestPriority: RequestPriority.High);
                        LatestReleases = new ObservableCollection<Game>(IgdbMatches);
                    }
                    catch (Exception e)
                    {
                        var m = e.Message;
                    }
                }),
                Task.Run(async () =>
                {
                    string queryString = null;
                    try
                    {
                        queryString = $"fields {Core.Models.Game.FullQueryFields}; where rating != null & platforms != null & platforms = (6); sort rating desc; limit 10;";
                        var IgdbMatches = await igdbClient.QueryAsync<Game>(Endpoints.Games, queryString, requestPriority: RequestPriority.High);
                        Top10RatedGames = new ObservableCollection<Game>(IgdbMatches);
                    }
                    catch (Exception e)
                    {
                        var m = e.Message;
                    }
                }),
                Task.Run(async () =>
                {
                    string queryString = null;
                    try
                    {
                        long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        queryString = $"fields {Core.Models.Game.FullQueryFields}; where first_release_date > {currentTimestamp} & hypes > 0 & platforms != null & platforms = (6); sort hypes desc; limit 10;";
                        var IgdbMatches = await igdbClient.QueryAsync<Game>(Endpoints.Games, queryString, requestPriority: RequestPriority.High);
                        MostAnticipated = new ObservableCollection<Game>(IgdbMatches.OrderBy(g => g.FirstReleaseDate));
                    }
                    catch (Exception e)
                    {
                        var m = e.Message;
                    }
                }));
        }
    }
}
