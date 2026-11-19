using FluentAssertions;
using GameStash.Core.Services;
using GameStash.Tests.Config;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Serilog;
using System.Linq;
using System.Threading.Tasks;

namespace GameStash.Core.Tests
{
    [TestFixture]
    public class EpicIntegrationTests
    {
        private IConfiguration _config;
        private string? _refreshToken;
        private string? _accessToken;

        [OneTimeSetUp]
        public void SetupOnce()
        {
            _config = new ConfigurationBuilder()
                .AddUserSecrets<EpicIntegrationTests>()
                .Build();

            // Read Epic credentials from user secrets
            _refreshToken = _config["epic_refresh_token"];
            _accessToken = _config["epic_access_token"];
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
        public async Task VerifySessionAsync_ShouldValidateAndPopulateUserSession()
        {
            // Arrange
            Assume.That(!string.IsNullOrWhiteSpace(_refreshToken) || !string.IsNullOrWhiteSpace(_accessToken),
                "Epic credentials are not set in user secrets.");

            var client = new EpicClient(_refreshToken, _accessToken);

            // Act
            var session = await client.VerifySessionAsync();

            // Assert
            session.Should().NotBeNull("VerifySessionAsync should succeed with a valid or refreshable token.");
            session!.AccountId.Should().NotBeNullOrWhiteSpace();
            Log.Information("Authenticated Epic AccountId: {AccountId} ({DisplayName})", session.AccountId, session.DisplayName);
        }

        [Test]
        public async Task FetchOwnedGamesAsync_ShouldReturnRealDataFromEpicApi()
        {
            // Arrange
            Assume.That(!string.IsNullOrWhiteSpace(_refreshToken) || !string.IsNullOrWhiteSpace(_accessToken),
                "Epic credentials are not set in user secrets.");

            var client = new EpicClient(_refreshToken, _accessToken);

            // Act
            var games = await client.FetchOwnedGamesAsync();

            // Assert
            games.Should().NotBeNull();
            games.Should().NotBeEmpty("Your account should have at least one item in the Epic Games library.");

            var firstGame = games.First();
            firstGame.Id.Should().NotBeNullOrWhiteSpace();
            firstGame.Title.Should().NotBeNullOrWhiteSpace();
            Log.Information("Fetched {Count} catalog items from Epic Games API. Sample: {Title}", games.Count, firstGame.Title);
        }

        [Test]
        public async Task FetchPlaytimeAsync_ShouldReturnPlaytimeDictionary()
        {
            // Arrange
            Assume.That(!string.IsNullOrWhiteSpace(_refreshToken) || !string.IsNullOrWhiteSpace(_accessToken),
                "Epic credentials are not set in user secrets.");

            var client = new EpicClient(_refreshToken, _accessToken);

            // Act
            var playtimeMap = await client.FetchPlaytimeAsync();

            // Assert
            playtimeMap.Should().NotBeNull();
            Log.Information("Retrieved playtime records for {Count} Epic artifacts.", playtimeMap.Count);

            if (playtimeMap.Any())
            {
                var topPlayed = playtimeMap.OrderByDescending(kvp => kvp.Value).First();
                Log.Information("Most played artifact: {ArtifactId} ({Minutes} mins)", topPlayed.Key, topPlayed.Value);
            }
        }

        [Test]
        public async Task FetchGameAchievementsAsync_ShouldReturnAchievementsWhenSupported()
        {
            // Arrange
            Assume.That(!string.IsNullOrWhiteSpace(_refreshToken) || !string.IsNullOrWhiteSpace(_accessToken),
                "Epic credentials are not set in user secrets.");

            var client = new EpicClient(_refreshToken, _accessToken);

            // Find a valid game namespace from the owned library (or replace with a specific sandboxId you own)
            var games = await client.FetchOwnedGamesAsync();
            var sampleGame = games.FirstOrDefault(g => !string.IsNullOrWhiteSpace(g.Namespace) && g.Namespace != "ue");
            Assume.That(sampleGame, Is.Not.Null, "No game with a valid Namespace found in library.");

            // Act
            var result = await client.FetchGameAchievementsAsync(sampleGame!.Namespace!);

            // Assert
            // Note: result can be null if the selected game does not implement Epic Achievements
            Log.Information("Checked achievements for {Title} (Namespace: {Namespace}). Has achievements: {HasAchievements}",
                sampleGame.Title, sampleGame.Namespace, result != null);

            if (result != null)
            {
                result.TotalAchievements.Should().BeGreaterThan(0);
                result.Achievements.Should().NotBeEmpty();
                Log.Information("Unlocked {Unlocked}/{Total} achievements ({EarnedXp}/{TotalXp} XP).",
                    result.UnlockedAchievements, result.TotalAchievements, result.EarnedXp, result.TotalXp);
            }
        }

        [Test]
        public void GetLocallyInstalledCatalogIds_ShouldScanLocalManifestsWithoutThrowing()
        {
            // Arrange
            var client = new EpicClient();

            // Act
            var installedIds = client.GetLocallyInstalledCatalogIds();

            // Assert
            installedIds.Should().NotBeNull();
            Log.Information("Detected {Count} locally installed Epic Games.", installedIds.Count);
        }
    }
}
