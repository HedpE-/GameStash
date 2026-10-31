using Common.Lib.Extensions;
using IgdbSharp;
using IgdbSharp.Enumerators;
using IgdbSharp.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace GameStash.Core.Models
{
    [DebuggerDisplay("{Id}, {Type}, {MatchName}, {Name}")]
    public class Game : IgdbSharp.Models.Game
    {
        public static readonly string FullQueryFields =
            "*,genres.*,platforms.*,platforms.platform_logo.*,cover.*,websites.*,screenshots.*,videos.*," +
            "similar_games.*,similar_games.cover.*,themes.*,game_modes.*,game_engines.*,player_perspectives.*," +
            "artworks.*,collections.*,involved_companies.*,involved_companies.company.*,involved_companies.company.logo.*," +
            "involved_companies.company.websites.*,game_type.*,game_status.*,rating,rating_count";

        public static readonly string AllQueryFields =
            "age_ratings,aggregated_rating,aggregated_rating_count,alternative_names,artworks,bundles,checksum," +
            "collections,cover,created_at,dlcs,expanded_games,expansions,external_games,first_release_date,forks," +
            "franchise,franchises,game_engines,game_localizations,game_modes,game_status,game_type,genres,hypes," +
            "involved_companies,keywords,language_supports,multiplayer_modes,name,parent_game,platforms," +
            "player_perspectives,ports,rating,rating_count,release_dates,remakes,remasters,screenshots,similar_games," +
            "slug,standalone_expansions,storyline,summary,tags,themes,total_rating,total_rating_count,updated_at,url," +
            "version_parent,version_title,videos,websites";

        private string _type;
        public string Type
        {
            get => _type;
            set => _type = value;
        }

        public InvolvedCompany DeveloperCompany
        {
            get
            {
                if (InvolvedCompanies?.Values != null && InvolvedCompanies.Values.Any())
                {
                    var dev = InvolvedCompanies.Values.FirstOrDefault(c => c.Developer == true);
                    return dev ?? InvolvedCompanies.Values.FirstOrDefault();
                }
                return null;
            }
        }

        private string _rootFolder;
        public string RootFolder
        {
            get => _rootFolder;
            set => _rootFolder = value;
        }

        private string _folder;
        public string Folder
        {
            get => _folder;
            set
            {
                if (_folder != value)
                {
                    _folder = value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        CheckIsFolderValid();
                        if (IsFolderValid)
                        {
                            try
                            {
                                FolderDiskSpaceBytes = new DirectoryInfo(value).GetDirectorySize(false);
                            }
                            catch
                            {
                                FolderDiskSpaceBytes = 0;
                            }
                        }
                    }
                }
            }
        }

        private long _folderDiskSpaceBytes;
        public long FolderDiskSpaceBytes
        {
            get => _folderDiskSpaceBytes;
            set => _folderDiskSpaceBytes = value;
        }

        private string _matchName;
        public string MatchName
        {
            get => _matchName;
            set => _matchName = value;
        }

        private string _sortName;
        public string SortName
        {
            get => _sortName;
            set => _sortName = value;
        }

        private DateTime? _dateAdded;
        public DateTime? DateAdded
        {
            get => _dateAdded;
            set => _dateAdded = value;
        }

        private bool _isIgdbMatched;
        public bool IsIgdbMatched
        {
            get => _isIgdbMatched;
            set => _isIgdbMatched = value;
        }

        private bool _isInstalled;
        public bool IsInstalled
        {
            get => _isInstalled;
            set => _isInstalled = value;
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => _isSelected = value;
        }

        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set => _isFavorite = value;
        }

        private bool _isFolderValid;
        public bool IsFolderValid
        {
            get
            {
                CheckIsFolderValid();
                return _isFolderValid;
            }
            set => _isFolderValid = value;
        }

        private bool _isLoadingIgdbMatches;
        public bool IsLoadingIgdbMatches
        {
            get => _isLoadingIgdbMatches;
            set => _isLoadingIgdbMatches = value;
        }

        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => _isRefreshing = value;
        }

        private IEnumerable<Game> _igdbMatches;
        public IEnumerable<Game> IgdbMatches
        {
            get => _igdbMatches;
            set => _igdbMatches = value;
        }

        private Game _igdbSelectedMatch;
        public Game IgdbSelectedMatch
        {
            get => _igdbSelectedMatch;
            set
            {
                if (_igdbSelectedMatch != value)
                {
                    _igdbSelectedMatch = value;
                    if (value != null)
                        IsSelected = true;
                }
            }
        }

        public Task MatchIgdbGame(string client_id, string secret, CancellationToken cancellationToken = default, RequestPriority requestPriority = RequestPriority.Medium)
        {
            var igdbClient = new IgdbClient(client_id, secret);
            return MatchIgdbGame(igdbClient, cancellationToken, requestPriority);
        }

        public async Task MatchIgdbGame(IgdbClient igdbClient, CancellationToken cancellationToken = default, RequestPriority requestPriority = RequestPriority.Medium)
        {
            IsLoadingIgdbMatches = true;

            try
            {
                var query = $"fields {FullQueryFields}; search \"{MatchName}\"; limit 50;";
                IgdbMatches = await igdbClient.QueryAsync<Game>(Endpoints.Games, query: query, cancellationToken: cancellationToken, requestPriority: requestPriority);
            }
            catch
            {
                IgdbMatches = Enumerable.Empty<Game>();
            }
            finally
            {
                IsLoadingIgdbMatches = false;
            }

            if (IgdbMatches?.Count() == 1)
            {
                IgdbSelectedMatch = IgdbMatches.First();
                if (IsIgdbMatched)
                    MatchIgdbGame(IgdbSelectedMatch);
            }
        }

        public void MatchIgdbGame(Game newGame)
        {
            if (newGame == null) return;

            Update(newGame);
            IsIgdbMatched = true;

            if (string.IsNullOrEmpty(SortName) && !string.IsNullOrEmpty(Name))
            {
                var regex = new Regex(@"^\s*(the|a|an)\s+", RegexOptions.IgnoreCase);
                SortName = regex.Replace(Name, "").Trim();
            }
        }

        public Task Refresh(string client_id, string secret, CancellationToken cancellationToken = default, RequestPriority requestPriority = RequestPriority.Medium)
        {
            var igdbClient = new IgdbClient(client_id, secret);
            return Refresh(igdbClient, cancellationToken, requestPriority);
        }

        public async Task Refresh(IgdbClient igdbClient, CancellationToken cancellationToken = default, RequestPriority requestPriority = RequestPriority.Medium)
        {
            try
            {
                IsRefreshing = true;

                if (Id.HasValue)
                    IgdbMatches = await igdbClient.QueryAsync<Game>(Endpoints.Games, query: $"fields {FullQueryFields}; where id = {Id};", cancellationToken: cancellationToken, requestPriority: requestPriority);
                else
                    IgdbMatches = await igdbClient.QueryAsync<Game>(Endpoints.Games, query: $"fields {FullQueryFields}; search \"{MatchName}\"; limit 500;", cancellationToken: cancellationToken, requestPriority: requestPriority);

                if (IgdbMatches?.Count() == 1)
                {
                    IgdbSelectedMatch = IgdbMatches.First();
                    if (IsIgdbMatched)
                        MatchIgdbGame(IgdbSelectedMatch);
                }
            }
            catch
            {
                // Suppress background refresh errors
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        public void CheckIsFolderValid()
        {
            IsFolderValid = !string.IsNullOrEmpty(Folder) && Directory.Exists(Folder);
        }

        public virtual void Update(Game game)
        {
            if (game == null) return;

            var properties = typeof(Game)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite);

            foreach (var property in properties)
            {
                try
                {
                    var val = property.GetValue(game);
                    property.SetValue(this, val);
                }
                catch
                {
                    // Suppress reflection assignment errors on computed properties
                }
            }
        }

        public static Task<IgdbSharp.Models.Game> GetBaseGame(string client_id, string secret, long id)
        {
            var igdbClient = new IgdbClient(client_id, secret);
            return GetBaseGame(igdbClient, id);
        }

        public static async Task<IgdbSharp.Models.Game> GetBaseGame(IgdbClient igdbClient, long id, CancellationToken cancellationToken = default, RequestPriority requestPriority = RequestPriority.Medium)
        {
            var igdbMatches = await igdbClient.QueryAsync<Game>(Endpoints.Games, query: $"fields {FullQueryFields}; where id = {id};", cancellationToken: cancellationToken, requestPriority: requestPriority);
            return igdbMatches?.FirstOrDefault();
        }
    }
}