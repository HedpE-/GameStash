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
    public class RecommendationService : IRecommendationService
    {
        private readonly ILocalStashService _stashService;

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

        public RecommendationService(ILocalStashService stashService)
        {
            _stashService = stashService;
        }

        public async Task<List<GameRecommendationDto>> GetPlaylistSuggestionsAsync(string libraryType, int limit = 10)
        {
            var allGames = await _stashService.GetStashAsync();
            var playlistGames = _stashService.GetPlaylist().ToList();

            if (!playlistGames.Any())
                return new List<GameRecommendationDto>();

            var playlistIds = playlistGames
                .Where(p => p.Id.HasValue)
                .Select(p => p.Id.Value)
                .ToHashSet();

            // Candidates: Games in Stash NOT in the playlist and NOT finished
            var candidatePool = allGames
                .Where(g => g.Id.HasValue && !playlistIds.Contains(g.Id.Value) && !g.IsFinished)
                .ToList();

            if (!candidatePool.Any())
                candidatePool = allGames.Where(g => g.Id.HasValue && !playlistIds.Contains(g.Id.Value)).ToList();

            var results = CalculateRecommendations(playlistGames, candidatePool, limit);

            if (results.Count < limit)
            {
                BackfillRemainingSlots(results, candidatePool, limit, "Playlist Queue Backlog");
            }

            return results;
        }

        public async Task<List<GameRecommendationDto>> GetStashSuggestionsAsync(string libraryType, int limit = 10)
        {
            var allGames = (await _stashService.GetStashAsync()).ToList();
            if (!allGames.Any())
                return new List<GameRecommendationDto>();

            // 1. Seeds: Games marked as Favorite, Currently Playing, or high rating
            var seedGames = allGames.Where(g =>
                g.IsFavorite ||
                g.IsPlaying ||
                (g.Rating.HasValue && g.Rating.Value >= 70.0)
            ).ToList();

            // 2. Cold Start Fallback: Top modern backlog games
            if (!seedGames.Any())
            {
                seedGames = allGames
                    .OrderByDescending(g => g.Rating ?? 0)
                    .ThenByDescending(g => g.FirstReleaseDate ?? DateTimeOffset.MinValue)
                    .Take(limit)
                    .ToList();
            }

            var seedIds = seedGames.Where(s => s.Id.HasValue).Select(s => s.Id.Value).ToHashSet();

            var candidatePool = allGames.Where(g =>
                g.Id.HasValue &&
                !seedIds.Contains(g.Id.Value) &&
                !g.IsFinished
            ).ToList();

            if (!candidatePool.Any())
                candidatePool = allGames.Where(g => g.Id.HasValue && !g.IsFinished).ToList();

            var results = CalculateRecommendations(seedGames, candidatePool, limit);

            if (results.Count < limit)
            {
                BackfillRemainingSlots(results, candidatePool, limit, "Top Rated in Stash");
            }

            return results;
        }

        public List<GameRecommendationDto> CalculateRecommendations(
            IEnumerable<LocalStashGame> seedGames,
            IEnumerable<LocalStashGame> candidatePool,
            int limit = 10)
        {
            var seeds = seedGames.ToList();
            var candidates = candidatePool.ToList();

            if (!seeds.Any() || !candidates.Any())
                return new List<GameRecommendationDto>();

            // Calculate average release year of the seed games to anchor era proximity
            var validSeedYears = seeds
                .Where(s => s.FirstReleaseDate.HasValue)
                .Select(s => s.FirstReleaseDate.Value.Year)
                .ToList();

            double targetAverageYear = validSeedYears.Any() ? validSeedYears.Average() : DateTime.Now.Year;

            // 1. Build Target Taste Vector from Seed Games
            var (tasteVector, tasteVectorMagnitude) = BuildTasteVector(seeds);

            if (tasteVectorMagnitude <= 0)
                return new List<GameRecommendationDto>();

            var scoredCandidates = new List<(LocalStashGame Candidate, double Score, double Similarity, List<string> Reasons)>();

            // 2. Score Each Candidate
            foreach (var candidate in candidates)
            {
                /* 
                // =========================================================================
                // PREQUEL BLOCK RULE (DESATIVADO TEMPORARIAMENTE)
                // Descomenta este bloco quando quiseres bloquear sequelas enquanto
                // os jogos anteriores da mesma saga não estiverem marcados como IsFinished:
                // =========================================================================
                if (IsUnfinishedPrequelInQueue(candidate, seeds))
                {
                    continue; 
                }
                */

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

                // Cosine Similarity
                double cosineSimilarity = dotProduct / (tasteVectorMagnitude * candidateMagnitude);

                // Multiplier 1: Era Proximity (Penaliza jogos de décadas muito distantes da queue)
                double eraMultiplier = CalculateEraMultiplier(candidate, targetAverageYear);

                // Multiplier 2: Friction bonus (+15% se o jogo já estiver instalado)
                double installMultiplier = candidate.IsInstalled ? 1.15 : 1.0;

                // Quality boost para jogos aclamados
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

                scoredCandidates.Add((candidate, finalScore, cosineSimilarity, topReasons));
            }

            // 3. Diversity Re-ranking: Teto por franquia para garantir variedade de géneros na strip
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
                    {
                        continue; // Passa ao próximo para dar espaço a outros estilos
                    }
                    franchiseCountTracker[franchiseKey] = currentCount + 1;
                }

                finalRecommendations.Add(new GameRecommendationDto
                {
                    Game = StashGameCardDto.FromModel(item.Candidate),
                    MatchScore = Math.Round(item.Score, 4),
                    MatchPercentage = Math.Min(99.0, Math.Round(item.Similarity * 100.0, 1)),
                    MatchReasons = item.Reasons
                });

                if (finalRecommendations.Count >= limit)
                    break;
            }

            return finalRecommendations;
        }

        private (Dictionary<string, double> Vector, double Magnitude) BuildTasteVector(List<LocalStashGame> seedGames)
        {
            var vector = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var game in seedGames)
            {
                double confidenceMultiplier = 1.0;
                if (game.IsFavorite) confidenceMultiplier += 0.8;
                if (game.IsPlaying) confidenceMultiplier += 0.8;

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

        private List<(string Key, double Weight, string DisplayLabel)> ExtractFeatures(LocalStashGame game)
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

        private static List<(long Id, string Name)> GetFranchises(LocalStashGame game)
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

        private static string GetPrimaryFranchiseGroup(LocalStashGame game)
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

        private static double CalculateEraMultiplier(LocalStashGame candidate, double targetAverageYear)
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
        /// Verifica se existe um jogo anterior da mesma saga na queue que ainda não foi terminado.
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
            List<LocalStashGame> candidates,
            int limit,
            string fallbackReason)
        {
            var existingIds = currentRecommendations
                .Where(r => r.Game?.Id != null)
                .Select(r => r.Game.Id)
                .ToHashSet();

            var backfillCandidates = candidates
                .Where(c => c.Id.HasValue && !existingIds.Contains(c.Id.Value))
                .OrderByDescending(c => c.IsInstalled)
                .ThenByDescending(c => c.FirstReleaseDate ?? DateTimeOffset.MinValue)
                .ThenByDescending(c => c.Rating ?? 0)
                .Take(limit - currentRecommendations.Count);

            foreach (var candidate in backfillCandidates)
            {
                currentRecommendations.Add(new GameRecommendationDto
                {
                    Game = StashGameCardDto.FromModel(candidate),
                    MatchScore = candidate.IsInstalled ? 0.70 : 0.55,
                    MatchPercentage = candidate.IsInstalled ? 70.0 : 55.0,
                    MatchReasons = new List<string> { candidate.IsInstalled ? "Ready to Play (Installed)" : fallbackReason }
                });
            }
        }
    }
}