using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameStash.Core.Models;
using GameStash.Core.Services;
using GameStash.Web.GUI.DTO;
using GameStash.Web.GUI.Interfaces;

namespace GameStash.Web.GUI.Services
{
    public class RecommendationService(
        ILocalStashService localStashService,
        ISteamStashService steamStashService,
        IEpicStashService epicStashService,
        IGogStashService gogStashService,
        IAmazonStashService amazonStashService) : IRecommendationService
    {
        private readonly ILocalStashService _localStashService = localStashService;
        private readonly ISteamStashService _steamStashService = steamStashService;
        private readonly IEpicStashService _epicStashService = epicStashService;
        private readonly IGogStashService _gogStashService = gogStashService;
        private readonly IAmazonStashService _amazonStashService = amazonStashService;


        // Domain Importance Weights
        private const double WeightFranchise = 3.5;
        private const double WeightDeveloper = 3.5;
        private const double WeightTheme = 3.0;
        private const double WeightGenre = 2.5;
        private const double WeightPerspective = 1.5;
        private const double WeightGameMode = 1.2;
        private const double WeightPlatform = 0.8;

        // Max games allowed from the same franchise in a single strip to guarantee diversity
        // Can be increased to 3 or more if desired
        private const int MaxGamesPerFranchise = 2;

        public async Task<List<GameRecommendationDto>> GetPlaylistSuggestionsAsync(int limit = 10)
        {
            var (allGames, playlistGames) = await GetUnifiedStashPoolAsync();

            if (!playlistGames.Any())
                return new List<GameRecommendationDto>();

            var playlistIds = playlistGames
                .Where(p => p.Game.Id.HasValue)
                .Select(p => p.Game.Id!.Value)
                .ToHashSet();

            // Candidates: Games across all stashes NOT in the playlist and NOT finished
            var candidatePool = allGames
                .Where(x => x.Game.Id.HasValue && !playlistIds.Contains(x.Game.Id.Value) && !x.Game.IsFinished && !x.Game.PlayedToEnd)
                .ToList();

            if (!candidatePool.Any())
                candidatePool = allGames.Where(x => x.Game.Id.HasValue && !playlistIds.Contains(x.Game.Id.Value)).ToList();

            var results = CalculateRecommendations(playlistGames, candidatePool, limit);

            if (results.Count < limit)
            {
                BackfillRemainingSlots(results, candidatePool, limit, "Playlist Queue Backlog");
            }

            return results;
        }

        public async Task<List<GameRecommendationDto>> GetStashSuggestionsAsync(int limit = 10)
        {
            var (allGames, _) = await GetUnifiedStashPoolAsync();
            if (!allGames.Any())
                return new List<GameRecommendationDto>();

            // 1. Seeds: Games marked as Favorite, Currently Playing, or high rating across all stashes
            // 1. Seeds: Games marked as Favorite, Currently Playing, high playtime (5h+), or high rating
            var seedGames = allGames.Where(x =>
                x.Game.IsFavorite ||
                x.Game.IsPlaying ||
                GetPlaytimeMinutes(x.Game) >= 300 ||
                (x.Game.Rating.HasValue && x.Game.Rating.Value >= 70.0)
            ).ToList();

            // 2. Cold Start Fallback: Top modern backlog games
            if (!seedGames.Any())
            {
                seedGames = allGames
                    .OrderByDescending(x => x.Game.Rating ?? 0)
                    .ThenByDescending(x => x.Game.FirstReleaseDate ?? DateTimeOffset.MinValue)
                    .Take(limit)
                    .ToList();
            }

            var seedIds = seedGames.Where(s => s.Game.Id.HasValue).Select(s => s.Game.Id!.Value).ToHashSet();

            var candidatePool = allGames.Where(x =>
                x.Game.Id.HasValue &&
                !seedIds.Contains(x.Game.Id.Value) &&
                !x.Game.IsFinished &&
                !x.Game.PlayedToEnd &&
                GetPlaytimeMinutes(x.Game) < 120
            ).ToList();

            if (!candidatePool.Any())
                candidatePool = allGames.Where(x => x.Game.Id.HasValue && !x.Game.IsFinished && !x.Game.PlayedToEnd).ToList();

            var results = CalculateRecommendations(seedGames, candidatePool, limit);

            if (results.Count < limit)
            {
                BackfillRemainingSlots(results, candidatePool, limit, "Top Rated in Stash");
            }

            return results;
        }

        public List<GameRecommendationDto> CalculateRecommendations(
            IEnumerable<(StashGame Game, string Source)> seedGames,
            IEnumerable<(StashGame Game, string Source)> candidatePool,
            int limit = 10)
        {
            var seeds = seedGames.ToList();
            var candidates = candidatePool.ToList();

            if (!seeds.Any() || !candidates.Any())
                return new List<GameRecommendationDto>();

            var validSeedYears = seeds
                .Where(s => s.Game.FirstReleaseDate.HasValue)
                .Select(s => s.Game.FirstReleaseDate!.Value.Year)
                .ToList();

            double targetAverageYear = validSeedYears.Any() ? validSeedYears.Average() : DateTime.Now.Year;

            var (tasteVector, tasteVectorMagnitude) = BuildTasteVector(seeds.Select(s => s.Game).ToList());
            if (tasteVectorMagnitude <= 0)
                return new List<GameRecommendationDto>();

            var scoredCandidates = new List<(StashGame Candidate, string Source, double Score, double Similarity, List<string> Reasons)>();

            // Deduplicate candidates across stores by positive IGDB ID (preferring installed copies)
            var deduplicatedCandidates = candidates
                .GroupBy(x => x.Game.Id!.Value)
                .Select(grp => grp.OrderByDescending(x => x.Game.IsInstalled).First())
                .ToList();

            foreach (var (candidate, source) in deduplicatedCandidates)
            {
                var candidateFeatures = ExtractFeatures(candidate);
                if (!candidateFeatures.Any())
                    continue;

                double dotProduct = 0;
                double candidateMagnitudeSquared = 0;
                var matchedReasons = new List<(string Reason, double Score)>();

                foreach (var (featureKey, featureWeight, displayLabel) in candidateFeatures)
                {
                    candidateMagnitudeSquared += featureWeight * featureWeight;

                    if (tasteVector.TryGetValue(featureKey, out var seedWeight))
                    {
                        var contribution = seedWeight * featureWeight;
                        dotProduct += contribution;
                        matchedReasons.Add((displayLabel, contribution));
                    }
                }

                if (dotProduct <= 0)
                    continue;

                double candidateMagnitude = Math.Sqrt(candidateMagnitudeSquared);
                if (candidateMagnitude <= 0)
                    continue;

                double cosineSimilarity = dotProduct / (tasteVectorMagnitude * candidateMagnitude);
                double eraMultiplier = CalculateEraMultiplier(candidate, targetAverageYear);
                double installMultiplier = candidate.IsInstalled ? 1.15 : 1.0;

                double qualityBoost = 0;
                if (candidate.Rating.HasValue && candidate.Rating.Value >= 75.0)
                {
                    qualityBoost = (candidate.Rating.Value - 75.0) * 0.002;
                }

                double finalScore = (cosineSimilarity * eraMultiplier * installMultiplier) + qualityBoost;

                var topReasons = matchedReasons
                    .OrderByDescending(r => r.Score)
                    .Select(r => r.Reason)
                    .Distinct()
                    .Take(2)
                    .ToList();

                scoredCandidates.Add((candidate, source, finalScore, cosineSimilarity, topReasons));
            }

            var finalRecommendations = new List<GameRecommendationDto>();
            var franchiseCountTracker = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var orderedCandidates = scoredCandidates
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Candidate.Rating ?? 0);

            foreach (var item in orderedCandidates)
            {
                string franchiseKey = GetPrimaryFranchiseGroup(item.Candidate);

                if (!string.IsNullOrEmpty(franchiseKey))
                {
                    franchiseCountTracker.TryGetValue(franchiseKey, out int currentCount);
                    if (currentCount >= MaxGamesPerFranchise)
                        continue;

                    franchiseCountTracker[franchiseKey] = currentCount + 1;
                }

                var dto = StashGameCardDto.FromModel(item.Candidate);
                dto.ExternalSystem = item.Source;

                finalRecommendations.Add(new GameRecommendationDto
                {
                    Game = dto,
                    MatchScore = Math.Round(item.Score, 4),
                    MatchPercentage = Math.Min(99.0, Math.Round(item.Similarity * 100.0, 1)),
                    MatchReasons = item.Reasons
                });

                if (finalRecommendations.Count >= limit)
                    break;
            }

            return finalRecommendations;
        }

        private async Task<(List<(StashGame Game, string Source)> AllGames, List<(StashGame Game, string Source)> PlaylistGames)> GetUnifiedStashPoolAsync()
        {
            var localGames = (await _localStashService.GetStashAsync()).Select(g => ((StashGame)g, "local"));
            var steamGames = (await _steamStashService.GetStashAsync()).Select(g => ((StashGame)g, "steam"));
            var epicGames = (await _epicStashService.GetStashAsync()).Select(g => ((StashGame)g, "epic"));
            var gogGames = (await _gogStashService.GetStashAsync()).Select(g => ((StashGame)g, "gog"));
            var amazonGames = (await _amazonStashService.GetStashAsync()).Select(g => ((StashGame)g, "amazon"));

            var all = localGames
                .Concat(steamGames)
                .Concat(epicGames)
                .Concat(gogGames)
                .Concat(amazonGames)
                .ToList();

            var playlist = all
                .Where(x => x.Item1.IsInPlaylist)
                .ToList();

            return (all, playlist);
        }

        private (Dictionary<string, double> Vector, double Magnitude) BuildTasteVector(List<StashGame> seedGames)
        {
            var vector = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var game in seedGames)
            {
                double confidenceMultiplier = 1.0;
                if (game.IsFavorite) confidenceMultiplier += 0.8;
                if (game.IsPlaying) confidenceMultiplier += 0.8;

                int mins = GetPlaytimeMinutes(game);
                if (mins >= 6000) confidenceMultiplier += 1.0;       // 100+ hours played
                else if (mins >= 1800) confidenceMultiplier += 0.7; // 30+ hours played
                else if (mins >= 600) confidenceMultiplier += 0.4;  // 10+ hours played

                var features = ExtractFeatures(game);
                foreach (var (featureKey, weight, _) in features)
                {
                    var weightedValue = weight * confidenceMultiplier;
                    if (vector.ContainsKey(featureKey))
                        vector[featureKey] += weightedValue;
                    else
                        vector[featureKey] = weightedValue;
                }
            }

            double sumSquares = vector.Values.Sum(v => v * v);
            double magnitude = Math.Sqrt(sumSquares);

            return (vector, magnitude);
        }

        private List<(string Key, double Weight, string DisplayLabel)> ExtractFeatures(StashGame game)
        {
            var features = new List<(string Key, double Weight, string DisplayLabel)>();

            // 1. Official IGDB Franchise & Franchises
            var franchises = GetFranchises(game);
            foreach (var (franchiseId, franchiseName) in franchises)
            {
                features.Add(($"franchise:{franchiseId}", WeightFranchise, $"Franchise: {franchiseName}"));
            }

            // 2. Official IGDB Collections (Game Series)
            if (game.Collections?.Values != null)
            {
                foreach (var col in game.Collections.Values.Where(c => !string.IsNullOrWhiteSpace(c?.Name)))
                {
                    features.Add(($"col:{col.Id ?? 0}", WeightFranchise, $"Series: {col.Name}"));
                }
            }
            else if (game.Collections?.Ids != null)
            {
                foreach (var id in game.Collections.Ids)
                {
                    features.Add(($"col:{id}", WeightFranchise, "Series Match"));
                }
            }

            // 3. Developer / Studio
            var dev = game.DeveloperCompany;
            var companyName = dev?.Company?.Value?.Name;
            var companyId = dev?.Company?.Id ?? dev?.Company?.Value?.Id;
            if (companyId.HasValue && companyId.Value > 0)
            {
                features.Add(($"dev:{companyId.Value}", WeightDeveloper, $"Studio: {companyName ?? "Developer"}"));
            }

            // 4. Atmospheric Themes
            if (game.Themes?.Values != null)
            {
                foreach (var theme in game.Themes.Values.Where(t => !string.IsNullOrWhiteSpace(t?.Name)))
                {
                    features.Add(($"theme:{theme.Id ?? 0}", WeightTheme, $"Theme: {theme.Name}"));
                }
            }
            else if (game.Themes?.Ids != null)
            {
                foreach (var id in game.Themes.Ids)
                {
                    features.Add(($"theme:{id}", WeightTheme, "Theme Match"));
                }
            }

            // 5. Core Genres
            if (game.Genres?.Values != null)
            {
                foreach (var genre in game.Genres.Values.Where(g => !string.IsNullOrWhiteSpace(g?.Name)))
                {
                    features.Add(($"genre:{genre.Id ?? 0}", WeightGenre, $"Genre: {genre.Name}"));
                }
            }
            else if (game.Genres?.Ids != null)
            {
                foreach (var id in game.Genres.Ids)
                {
                    features.Add(($"genre:{id}", WeightGenre, "Genre Match"));
                }
            }

            // 6. Perspectives
            if (game.PlayerPerspectives?.Values != null)
            {
                foreach (var p in game.PlayerPerspectives.Values.Where(p => !string.IsNullOrWhiteSpace(p?.Name)))
                {
                    features.Add(($"perspective:{p.Id ?? 0}", WeightPerspective, $"Perspective: {p.Name}"));
                }
            }

            // 7. Game Modes
            if (game.GameModes?.Values != null)
            {
                foreach (var m in game.GameModes.Values.Where(m => !string.IsNullOrWhiteSpace(m?.Name)))
                {
                    features.Add(($"mode:{m.Id ?? 0}", WeightGameMode, $"Mode: {m.Name}"));
                }
            }

            // 8. Platforms
            if (game.Platforms?.Values != null)
            {
                foreach (var platform in game.Platforms.Values.Where(p => p.Id.HasValue))
                {
                    features.Add(($"platform:{platform.Id.Value}", WeightPlatform, "Platform"));
                }
            }

            return features;
        }

        private static int GetPlaytimeMinutes(StashGame game) => game switch
        {
            SteamStashGame s => s.PlaytimeMinutes,
            EpicStashGame e => e.PlaytimeMinutes,
            GogStashGame g => g.PlaytimeMinutes,
            _ => 0
        };

        private static List<(long Id, string Name)> GetFranchises(StashGame game)
        {
            var list = new List<(long Id, string Name)>();

            // Single Franchise
            if (game.Franchise?.Value != null && !string.IsNullOrWhiteSpace(game.Franchise.Value.Name))
            {
                list.Add((game.Franchise.Value.Id ?? 0, game.Franchise.Value.Name.Trim()));
            }

            // Multiple Franchises
            if (game.Franchises?.Values != null)
            {
                foreach (var f in game.Franchises.Values.Where(f => !string.IsNullOrWhiteSpace(f?.Name)))
                {
                    if (!list.Any(x => x.Id == f.Id))
                        list.Add((f.Id ?? 0, f.Name.Trim()));
                }
            }

            // Safe fallback: detect clean known series prefix if IGDB franchise is unlinked
            if (!list.Any() && !string.IsNullOrWhiteSpace(game.Name))
            {
                var detected = DetectKnownFranchise(game.Name);
                if (!string.IsNullOrEmpty(detected))
                {
                    list.Add((detected.GetHashCode(), detected));
                }
            }

            return list;
        }

        private static string GetPrimaryFranchiseGroup(StashGame game)
        {
            if (game.Franchise?.Value != null && !string.IsNullOrWhiteSpace(game.Franchise.Value.Name))
                return game.Franchise.Value.Name.Trim();

            var firstFranchise = game.Franchises?.Values?.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f?.Name));
            if (firstFranchise != null)
                return firstFranchise.Name.Trim();

            var firstCol = game.Collections?.Values?.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c?.Name));
            if (firstCol != null)
                return firstCol.Name.Trim();

            return DetectKnownFranchise(game.Name);
        }

        private static string DetectKnownFranchise(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var clean = name.Trim();

            if (clean.StartsWith("Final Fantasy", StringComparison.OrdinalIgnoreCase)) return "Final Fantasy";
            if (clean.StartsWith("Star Wars", StringComparison.OrdinalIgnoreCase)) return "Star Wars";
            if (clean.StartsWith("Assassin's Creed", StringComparison.OrdinalIgnoreCase) || clean.StartsWith("Assassins Creed", StringComparison.OrdinalIgnoreCase)) return "Assassin's Creed";
            if (clean.StartsWith("Tomb Raider", StringComparison.OrdinalIgnoreCase)) return "Tomb Raider";
            if (clean.StartsWith("Resident Evil", StringComparison.OrdinalIgnoreCase)) return "Resident Evil";
            if (clean.StartsWith("Metal Gear", StringComparison.OrdinalIgnoreCase)) return "Metal Gear";
            if (clean.StartsWith("Dark Souls", StringComparison.OrdinalIgnoreCase)) return "Dark Souls";
            if (clean.StartsWith("The Witcher", StringComparison.OrdinalIgnoreCase) || clean.StartsWith("Witcher", StringComparison.OrdinalIgnoreCase)) return "The Witcher";
            if (clean.StartsWith("Far Cry", StringComparison.OrdinalIgnoreCase)) return "Far Cry";

            // If game uses subtitle separator (e.g., "Hitman: Blood Money" -> "Hitman")
            if (clean.Contains(':'))
            {
                var prefix = clean.Split(':')[0].Trim();
                if (prefix.Length >= 4 && prefix.Length <= 25)
                    return prefix;
            }

            return string.Empty;
        }

        private static double CalculateEraMultiplier(StashGame candidate, double targetAverageYear)
        {
            if (!candidate.FirstReleaseDate.HasValue || targetAverageYear <= 0)
                return 1.0;

            int candidateYear = candidate.FirstReleaseDate.Value.Year;
            double yearDiff = Math.Abs(candidateYear - targetAverageYear);

            if (yearDiff <= 4) return 1.10;   // In the exact same era (+10% bonus)
            if (yearDiff <= 8) return 1.0;    // Modern contemporary
            if (yearDiff <= 14) return 0.85;  // Previous generation
            if (yearDiff <= 20) return 0.65;  // 2 generations ago
            return 0.45;                      // Retro era penalty (> 20 years difference)
        }

        /*
        /// <summary>
        /// Checks whether an earlier game from the same series is unfinished in the queue.
        /// </summary>
        private static bool IsUnfinishedPrequelInQueue(LocalStashGame candidate, List<LocalStashGame> seeds)
        {
            if (!candidate.FirstReleaseDate.HasValue) return false;

            string candidateFranchise = GetPrimaryFranchiseGroup(candidate);
            if (string.IsNullOrEmpty(candidateFranchise)) return false;

            return seeds.Any(s =>
                s.Id != candidate.Id &&
                !s.IsFinished &&
                s.FirstReleaseDate.HasValue &&
                s.FirstReleaseDate.Value < candidate.FirstReleaseDate.Value &&
                GetPrimaryFranchiseGroup(s).Equals(candidateFranchise, StringComparison.OrdinalIgnoreCase)
            );
        }
        */

        private static void BackfillRemainingSlots(
    List<GameRecommendationDto> currentRecommendations,
    List<(StashGame Game, string Source)> candidates,
    int limit,
    string fallbackReason)
        {
            var existingIds = currentRecommendations
                .Where(r => r.Game?.Id != null)
                .Select(r => r.Game.Id)
                .ToHashSet();

            var backfillCandidates = candidates
                .Where(c => c.Game.Id.HasValue && !existingIds.Contains(c.Game.Id.Value))
                .GroupBy(c => c.Game.Id!.Value)
                .Select(grp => grp.OrderByDescending(c => c.Game.IsInstalled).First())
                .OrderByDescending(c => c.Game.IsInstalled)
                .ThenByDescending(c => c.Game.FirstReleaseDate ?? DateTimeOffset.MinValue)
                .ThenByDescending(c => c.Game.Rating ?? 0)
                .Take(limit - currentRecommendations.Count);

            foreach (var item in backfillCandidates)
            {
                var dto = StashGameCardDto.FromModel(item.Game);
                dto.ExternalSystem = item.Source;

                currentRecommendations.Add(new GameRecommendationDto
                {
                    Game = dto,
                    MatchScore = item.Game.IsInstalled ? 0.70 : 0.55,
                    MatchPercentage = item.Game.IsInstalled ? 70.0 : 55.0,
                    MatchReasons = new List<string> { item.Game.IsInstalled ? "Ready to Play (Installed)" : fallbackReason }
                });
            }
        }
    }
}