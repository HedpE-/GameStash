using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using IgdbSharp.Models;

namespace GameStash.Core.Services
{
    public static class GameMatchScorer
    {
        private static readonly Regex YearRegex = new(@"\b(19\d{2}|20\d{2})\b", RegexOptions.Compiled);
        private static readonly Regex PunctuationRegex = new(@"[^\w\s]", RegexOptions.Compiled);
        private static readonly Regex MultipleSpacesRegex = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex RomanNumeralsRegex = new(@"\b(i|ii|iii|iv|v|vi|vii|viii|ix|x)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Calculates a match probability between 0.0 (0%) and 1.0 (100%).
        /// </summary>
        public static double CalculateProbability(string matchName, Game candidate)
        {
            if (string.IsNullOrWhiteSpace(matchName) || string.IsNullOrWhiteSpace(candidate?.Name))
                return 0.0;

            // 1. Extract optional release year from MatchName (e.g., "Doom (2016)")
            int? extractedYear = null;
            var yearMatch = YearRegex.Match(matchName);
            if (yearMatch.Success && int.TryParse(yearMatch.Value, out int y))
            {
                extractedYear = y;
            }

            // 2. Normalize both strings for fuzzy comparison
            string normalizedInput = CleanString(matchName);
            string normalizedCandidate = CleanString(candidate.Name);

            // Exact normalized match -> starts at maximum confidence
            if (string.Equals(normalizedInput, normalizedCandidate, StringComparison.OrdinalIgnoreCase))
            {
                return ApplyContextAdjustments(1.0, candidate, extractedYear);
            }

            // 3. Compute Levenshtein character similarity (0.0 to 1.0)
            double levSim = ComputeLevenshteinSimilarity(normalizedInput, normalizedCandidate);

            // 4. Compute Word Token Overlap (Sørensen–Dice coefficient)
            double tokenSim = ComputeTokenOverlap(normalizedInput, normalizedCandidate);

            // 5. Blended base text score (weighted 50% character edit distance, 50% token overlap)
            double baseScore = (levSim * 0.50) + (tokenSim * 0.50);

            // 6. Apply year, game type, and popularity bonuses/penalties
            return ApplyContextAdjustments(baseScore, candidate, extractedYear);
        }

        private static double ApplyContextAdjustments(double baseScore, Game candidate, int? extractedYear)
        {
            double score = baseScore;

            // Year Boost/Penalty (if folder had a year)
            if (extractedYear.HasValue && candidate.FirstReleaseDate.HasValue)
            {
                int candidateYear = candidate.FirstReleaseDate.Value.Year;
                if (candidateYear == extractedYear.Value)
                    score += 0.15; // Strong boost for exact year match
                else if (Math.Abs(candidateYear - extractedYear.Value) <= 1)
                    score += 0.05; // Adjacent year tolerance (delayed releases/ports)
                else
                    score -= 0.20; // Penalty if year differs significantly
            }

            // Prefer Main Game over DLCs / Expansions / Ports / Mods
            // In IGDB: GameType 0 is "Main Game"
            if (candidate.GameType?.Id == 0)
            {
                score += 0.05;
            }
            else if (candidate.GameType?.Id is 1 or 2 or 3) // DLC, Expansion, Bundle
            {
                score -= 0.05;
            }

            // Popularity / Rating count tie-breaker (up to +0.03)
            if (candidate.RatingCount.HasValue && candidate.RatingCount.Value > 0)
            {
                // Logarithmic scaling: 10 ratings ~ +0.01, 100 ratings ~ +0.02, 1000+ ~ +0.03
                double popularityBoost = Math.Min(0.03, Math.Log10(candidate.RatingCount.Value) * 0.01);
                score += popularityBoost;
            }

            return Math.Clamp(score, 0.0, 1.0);
        }

        private static string CleanString(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            // Remove 4-digit years from comparison text
            string withoutYear = YearRegex.Replace(text, " ");

            // Convert to lowercase
            string lower = withoutYear.ToLowerInvariant();

            // Normalize Roman numerals to arabic numbers for consistency (e.g., "III" -> "3")
            lower = RomanNumeralsRegex.Replace(lower, m => m.Value.ToLowerInvariant() switch
            {
                "i" => "1",
                "ii" => "2",
                "iii" => "3",
                "iv" => "4",
                "v" => "5",
                "vi" => "6",
                "vii" => "7",
                "viii" => "8",
                "ix" => "9",
                "x" => "10",
                _ => m.Value
            });

            // Strip symbols & punctuation, leaving only alphanumeric and spaces
            string clean = PunctuationRegex.Replace(lower, " ");

            // Collapse multiple spaces
            return MultipleSpacesRegex.Replace(clean, " ").Trim();
        }

        private static double ComputeLevenshteinSimilarity(string s1, string s2)
        {
            if (s1 == s2) return 1.0;
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;

            int maxLen = Math.Max(s1.Length, s2.Length);
            if (maxLen == 0) return 1.0;

            int distance = LevenshteinDistance(s1, s2);
            return 1.0 - ((double)distance / maxLen);
        }

        private static int LevenshteinDistance(string s1, string s2)
        {
            var costs = new int[s2.Length + 1];
            for (int j = 0; j <= s2.Length; j++) costs[j] = j;

            for (int i = 1; i <= s1.Length; i++)
            {
                int previousCost = costs[0];
                costs[0] = i;

                for (int j = 1; j <= s2.Length; j++)
                {
                    int currentCost = costs[j];
                    int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;

                    costs[j] = Math.Min(
                        Math.Min(costs[j] + 1, costs[j - 1] + 1),
                        previousCost + cost
                    );

                    previousCost = currentCost;
                }
            }

            return costs[s2.Length];
        }

        private static double ComputeTokenOverlap(string s1, string s2)
        {
            var tokens1 = new HashSet<string>(s1.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            var tokens2 = new HashSet<string>(s2.Split(' ', StringSplitOptions.RemoveEmptyEntries));

            if (tokens1.Count == 0 || tokens2.Count == 0) return 0.0;

            int intersection = tokens1.Count(t => tokens2.Contains(t));
            return (2.0 * intersection) / (tokens1.Count + tokens2.Count);
        }
    }
}