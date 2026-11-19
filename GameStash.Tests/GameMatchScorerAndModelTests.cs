using FluentAssertions;
using GameStash.Core.Models;
using GameStash.Core.Services;
using GameStash.Tests.Config;
using IgdbSharp.Models;
using IgdbSharp.Serialization;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Serilog;

namespace GameStash.Core.Tests
{
    [TestFixture]
    public class GameMatchScorerAndModelTests
    {
        private IConfiguration _config;
        private string? _igdbClientId;
        private string? _igdbSecret;

        [OneTimeSetUp]
        public void SetupOnce()
        {
            _config = new ConfigurationBuilder()
                .AddUserSecrets<GameMatchScorerAndModelTests>()
                .Build();

            _igdbClientId = _config["igdb_client_id"];
            _igdbSecret = _config["igdb_secret"];
        }

        [SetUp]
        public void Setup()
        {
            TestLogInitializer.InitializeTestLogger();
        }

        [OneTimeTearDown]
        public void OneTimeTeardown()
        {
            Log.CloseAndFlush();
        }

        [Test]
        public void CalculateProbability_ExactMatch_ShouldReturnHighConfidence()
        {
            var candidate = new Game { Name = "Hollow Knight", GameType = new IdentityOrValue<GameType>(0) };

            double score = GameMatchScorer.CalculateProbability("Hollow Knight", candidate);

            score.Should().Be(1.0);
        }

        [Test]
        public void CalculateProbability_RomanNumerals_ShouldNormalizeToArabicNumbers()
        {
            var candidate = new Game { Name = "Dark Souls 3" };

            double score = GameMatchScorer.CalculateProbability("Dark Souls III", candidate);

            score.Should().BeGreaterThanOrEqualTo(0.95);
        }

        [Test]
        public void CalculateProbability_WithYearInQuery_ShouldBoostMatchingYearAndPenalizeMismatch()
        {
            var doom2016 = new Game
            {
                Name = "DOOM",
                FirstReleaseDate = new DateTimeOffset(2016, 5, 13, 0, 0, 0, TimeSpan.Zero)
            };

            var doom1993 = new Game
            {
                Name = "DOOM",
                FirstReleaseDate = new DateTimeOffset(1993, 12, 10, 0, 0, 0, TimeSpan.Zero)
            };

            double score2016 = GameMatchScorer.CalculateProbability("Doom (2016)", doom2016);
            double score1993 = GameMatchScorer.CalculateProbability("Doom (2016)", doom1993);

            score2016.Should().BeGreaterThan(score1993);
            score2016.Should().Be(1.0);
            score1993.Should().BeLessThanOrEqualTo(0.85);
        }

        [Test]
        public void CalculateProbability_MainGameVsDlc_ShouldPreferMainGame()
        {
            var mainGame = new Game { Name = "Control", GameType = new IdentityOrValue<GameType>(0) };
            var dlcGame = new Game { Name = "Control", GameType = new IdentityOrValue<GameType>(1) };

            double mainScore = GameMatchScorer.CalculateProbability("Control", mainGame);
            double dlcScore = GameMatchScorer.CalculateProbability("Control", dlcGame);

            mainScore.Should().BeGreaterThan(dlcScore);
        }

        [Test]
        public void LocalStashGame_MatchIgdbGame_ShouldGenerateSortNameWithoutArticles()
        {
            var localGame = new LocalStashGame();
            var igdbGame = new Game { Id = 1942, Name = "The Witcher 3: Wild Hunt" };

            localGame.MatchIgdbGame(igdbGame);

            localGame.IsIgdbMatched.Should().BeTrue();
            localGame.Id.Should().Be(1942);
            localGame.SortName.Should().Be("Witcher 3: Wild Hunt");
        }

        [Test]
        public async Task LocalStashGame_FolderProperty_ShouldValidateExistenceAndComputeDiskSpace()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "GameStash_LocalModel_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dummyFile = Path.Combine(tempDir, "data.bin");
                await File.WriteAllBytesAsync(dummyFile, new byte[2048]);

                var localGame = new LocalStashGame { Folder = tempDir };

                localGame.IsFolderValid.Should().BeTrue();
                localGame.FolderDiskSpaceBytes.Should().Be(2048);

                Directory.Delete(tempDir, recursive: true);

                localGame.IsFolderValid.Should().BeFalse();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }

        [Test]
        [Category("Integration")]
        public async Task LocalStashGame_RefreshFromIgdb_ShouldPopulateFullMetadata()
        {
            Assume.That(!string.IsNullOrWhiteSpace(_igdbClientId), "IGDB Client ID not set in user secrets.");
            Assume.That(!string.IsNullOrWhiteSpace(_igdbSecret), "IGDB Secret not set in user secrets.");

            var game = new LocalStashGame { Id = 140427 };

            await game.Refresh(_igdbClientId!, _igdbSecret!);
            game.MatchIgdbGame(game.IgdbSelectedMatch);

            game.IsIgdbMatched.Should().BeTrue();
            game.Name.Should().NotBeNullOrWhiteSpace();
            game.SortName.Should().NotBeNullOrWhiteSpace();
        }
    }
}
