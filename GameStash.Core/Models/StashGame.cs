using Common.Lib.Extensions;
using GameStash.Core.Extensions;
using GameStash.Core.Services;
using IgdbSharp;
using IgdbSharp.Enumerators;
using IgdbSharp.Models;
using IgdbSharp.Serialization;
using Serilog;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace GameStash.Core.Models
{
    [DebuggerDisplay("{Id}, {Type}, {MatchName}, {Name}")]
    public abstract class StashGame : Game
    {
        public static readonly string FullQueryFields =
            "*,genres.*,platforms.*,platforms.platform_logo.*,cover.*,websites.*,screenshots.*,videos.*," +
            "similar_games.*,similar_games.cover.*,themes.*,game_modes.*,game_engines.*,player_perspectives.*," +
            "artworks.*,collections.*,involved_companies.*,involved_companies.company.*,involved_companies.company.logo.*," +
            "involved_companies.company.websites.*,franchise,franchises.*,game_type.*,game_status.*,rating,rating_count";

        public static readonly string AllQueryFields =
            "age_ratings,aggregated_rating,aggregated_rating_count,alternative_names,artworks,bundles,checksum," +
            "collections,cover,created_at,dlcs,expanded_games,expansions,external_games,first_release_date,forks," +
            "franchise,franchises,game_engines,game_localizations,game_modes,game_status,game_type,genres,hypes," +
            "involved_companies,keywords,language_supports,multiplayer_modes,name,parent_game,platforms," +
            "player_perspectives,ports,rating,rating_count,release_dates,remakes,remasters,screenshots,similar_games," +
            "slug,standalone_expansions,storyline,summary,tags,themes,total_rating,total_rating_count,updated_at,url," +
            "version_parent,version_title,videos,websites";

        public static readonly string GameCardQueryFields = "id, name, cover.*";
        public static readonly string ReleaseDateQueryField = "first_release_date";
        public static readonly string RatingQueryFields = "rating,rating_count,total_rating_count";

        public static readonly string StashGameCardQueryFields = $"{GameCardQueryFields},{ReleaseDateQueryField},{RatingQueryFields},franchise,franchises.*";

        public static readonly string MatchGameQueryFields = $"{StashGameCardQueryFields},platforms.*,platforms.platform_logo.*,game_type.*,game_status.*";
        public static readonly string GameSearchQueryFields = $"{GameCardQueryFields},{ReleaseDateQueryField},{RatingQueryFields}, platforms.*,platforms.platform_logo.*, game_type.*," +
            " involved_companies.*, involved_companies.company.*,involved_companies.company.logo.*,involved_companies.company.websites.*,involved_companies.game.*";

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

        private string _matchName;
        public string MatchName
        {
            get => _matchName;
            set => _matchName = value;
        }

        private string _sortName = string.Empty;
        public string SortName
        {
            get => _sortName;
            set => _sortName = value;
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

        private bool _isPlayed;
        public bool IsPlayed
        {
            get => _isPlayed;
            set => _isPlayed = value;
        }

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set => _isPlaying = value;
        }

        private bool _isFinished;
        public bool IsFinished
        {
            get => _isFinished;
            set => _isFinished = value;
        }

        private bool _playedToEnd;
        public bool PlayedToEnd
        {
            get => _playedToEnd;
            set => _playedToEnd = value;
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
                    if (value != null && IgdbMatches.Count() == 1)
                        IsSelected = true;
                    else
                        IsSelected = false;
                }
            }
        }

        private bool _isInPlaylist;
        public bool IsInPlaylist
        {
            get => _isInPlaylist;
            set => _isInPlaylist = value;
        }

        public string ReleaseDate => this.GetReleaseDate("dd MMM, yyyy");

        public Task MatchIgdbGame(string client_id, string secret, int? limit = null, CancellationToken cancellationToken = default, RequestPriority requestPriority = RequestPriority.Medium)
        {
            var igdbClient = new IgdbClient(client_id, secret);
            return MatchIgdbGame(igdbClient, limit, cancellationToken, requestPriority);
        }

        public async Task MatchIgdbGame(IgdbClient igdbClient, int? limit = null, CancellationToken cancellationToken = default, RequestPriority requestPriority = RequestPriority.Medium)
        {
            IsLoadingIgdbMatches = true;

            try
            {
                var query = $"fields {MatchGameQueryFields}; where game_type != 1; search \"{MatchName}\";";
                var results = await igdbClient.QueryAsync<Game>(Endpoints.Games, query: query, limitResults: limit, cancellationToken: cancellationToken, requestPriority: requestPriority);

                if (results != null && results.Any())
                {
                    // Order results by probability calculation descending
                    IgdbMatches = results
                        .OrderByDescending(candidate => GameMatchScorer.CalculateProbability(MatchName, candidate))
                        .ThenByDescending(candidate => candidate.Platforms?.Values?.Any(p => p.Id == 6) == true)
                        .ThenByDescending(candidate => candidate.FirstReleaseDate ?? null);
                }
                else
                {
                    IgdbMatches = Enumerable.Empty<Game>();
                }
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
                    var incomingVal = property.GetValue(game);
                    if (incomingVal == null) continue;

                    var currentVal = property.GetValue(this);

                    if (currentVal == null)
                    {
                        property.SetValue(this, incomingVal);
                        continue;
                    }

                    if (IsIdentityType(property.PropertyType))
                    {
                        HasIdentityContent(incomingVal, out bool incomingHasValues);
                        if (incomingHasValues)
                        {
                            property.SetValue(this, incomingVal);
                        }
                    }
                    else
                    {
                        property.SetValue(this, incomingVal);
                    }
                }
                catch (Exception e)
                {
                    Log.ForContext<StashGame>().Error(e, "Failed to update property {PropertyName} on Game model.", property.Name);
                }
            }
        }

        /// <summary>
        /// Verifica se o tipo da propriedade é IdentityOrValue<T> ou IdentitiesOrValues<T>.
        /// </summary>
        private static bool IsIdentityType(Type type)
        {
            if (!type.IsGenericType) return false;
            var genericDef = type.GetGenericTypeDefinition();
            return genericDef == typeof(IdentityOrValue<>) || genericDef == typeof(IdentitiesOrValues<>);
        }

        /// <summary>
        /// Extrai reflexivamente se o objeto possui IDs ou Values hidratados.
        /// </summary>
        private static bool HasIdentityContent(object obj, out bool hasValues)
        {
            hasValues = false;
            if (obj == null) return false;

            var type = obj.GetType();
            if (!type.IsGenericType) return false;

            var genericDef = type.GetGenericTypeDefinition();

            // Caso 1: IdentityOrValue<T>
            if (genericDef == typeof(IdentityOrValue<>))
            {
                var val = type.GetProperty("Value")?.GetValue(obj);
                var id = type.GetProperty("Id")?.GetValue(obj);

                hasValues = val != null;
                return hasValues || id != null;
            }

            // Caso 2: IdentitiesOrValues<T>
            if (genericDef == typeof(IdentitiesOrValues<>))
            {
                var values = type.GetProperty("Values")?.GetValue(obj) as Array;
                var ids = type.GetProperty("Ids")?.GetValue(obj) as long[];

                hasValues = values != null && values.Length > 0;
                return hasValues || (ids != null && ids.Length > 0);
            }

            return false;
        }

        public async Task Reset(IgdbClient igdbClient)
        {
            Game game;
            if (Id.HasValue && !string.IsNullOrEmpty(Name) && Platforms?.Values != null && FirstReleaseDate.HasValue && Rating != null)
                game = this.Clone();
            else
                game = await this.GetBaseGame(igdbClient, StashGame.MatchGameQueryFields);
            if (IgdbMatches == null || !IgdbMatches.Any())
            {
                IgdbMatches = [game];
            }
            else
            {
                IgdbMatches = IgdbMatches.Append(game)
                        .OrderByDescending(candidate => GameMatchScorer.CalculateProbability(MatchName, candidate))
                        .ThenByDescending(candidate => candidate.Name ?? null)
                        .ThenByDescending(candidate => candidate.FirstReleaseDate ?? null);
            }

            IgdbMatches = [game];
            IgdbSelectedMatch = game;

            if (string.IsNullOrEmpty(MatchName))
                MatchName = Name ?? SortName;

            PurgeIgdbData();
            IsIgdbMatched = false;
        }

        public virtual void PurgeIgdbData()
        {
            var properties = typeof(Game)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite);

            foreach (var property in properties)
            {
                try
                {
                    property.SetValue(this, null);
                }
                catch (Exception e)
                {
                    Log.ForContext<StashGame>().Error(e, "Failed to update property {PropertyName} on Game model.", property.Name);
                }
            }
        }

        public static Task<Game> GetBaseGame(string client_id, string secret, long id)
        {
            var igdbClient = new IgdbClient(client_id, secret);

            return GetBaseGame(igdbClient, id);
        }

        public static async Task<Game> GetBaseGame(IgdbClient igdbClient, long id)
        {
            var igdbMatches = await igdbClient.QueryAsync<Game>(Endpoints.Games, query: $"fields {StashGame.FullQueryFields}; where id = {id};");

            if (igdbMatches?.Count() > 0)
            {
                return igdbMatches.First();
            }

            return null;
        }
    }
}