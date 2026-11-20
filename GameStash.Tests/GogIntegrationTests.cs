using FluentAssertions;
using GameStash.Core.Services;
using GameStash.Tests.Config;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Serilog;

namespace GameStash.Core.Tests
{
    [TestFixture]
    [Category("Integration")]
    public class GogIntegrationTests
    {
        private IConfiguration _config;
        private string? _refreshToken;
        private string? _accessToken;

        [OneTimeSetUp]
        public void SetupOnce()
        {
            _config = new ConfigurationBuilder()
                .AddUserSecrets<GogIntegrationTests>()
                .Build();

            // Read GOG credentials from user secrets
            _refreshToken = _config["gog_refresh_token"];
            _accessToken = _config["gog_access_token"];
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
        public async Task GetUserDataAsync_ShouldValidateAndReturnUserProfile()
        {
            // Arrange
            Assume.That(!string.IsNullOrWhiteSpace(_refreshToken) || !string.IsNullOrWhiteSpace(_accessToken),
                "GOG credentials are not set in user secrets.");

            var client = new GogClient(_refreshToken, _accessToken);

            // Act
            var userData = await client.GetUserDataAsync();

            // Assert
            userData.Should().NotBeNull("GetUserDataAsync should succeed with a valid or refreshable token.");
            userData!.IsLoggedIn.Should().BeTrue();
            userData.UserId.Should().NotBeNullOrWhiteSpace();
            Log.Information("Authenticated GOG User: {Username} (UserId: {UserId})", userData.Username, userData.UserId);
        }

        [Test]
        public async Task FetchOwnedGamesAsync_ShouldReturnAllPagesFromGogApi()
        {
            // Arrange
            Assume.That(!string.IsNullOrWhiteSpace(_refreshToken) || !string.IsNullOrWhiteSpace(_accessToken),
                "GOG credentials are not set in user secrets.");

            var client = new GogClient(_refreshToken, _accessToken);

            // Act
            var games = await client.FetchOwnedGamesAsync();

            // Assert
            games.Should().NotBeNull();
            games.Should().NotBeEmpty("Your account should have at least one game on GOG.");

            var firstGame = games.First();
            firstGame.Id.Should().BeGreaterThan(0);
            firstGame.Title.Should().NotBeNullOrWhiteSpace();
            Log.Information("Fetched {Count} products from GOG API. Sample: {Title} (ID: {Id})",
                games.Count, firstGame.Title, firstGame.Id);
        }

        [Test]
        public async Task FetchPlaytimeAsync_ShouldReturnPlaytimeDictionary()
        {
            // Arrange
            Assume.That(!string.IsNullOrWhiteSpace(_refreshToken) || !string.IsNullOrWhiteSpace(_accessToken),
                "GOG credentials are not set in user secrets.");

            var client = new GogClient(_refreshToken, _accessToken);

            // Act
            var playtimeMap = await client.FetchPlaytimeAsync();

            // Assert
            playtimeMap.Should().NotBeNull();
            Log.Information("Retrieved playtime records for {Count} GOG games.", playtimeMap.Count);

            if (playtimeMap.Any())
            {
                var topPlayed = playtimeMap.OrderByDescending(kvp => kvp.Value).First();
                Log.Information("Most played GOG Game ID: {GameId} ({Minutes} mins)", topPlayed.Key, topPlayed.Value);
            }
        }

        [Test]
        public async Task FetchGameAchievementsAsync_ShouldReturnAchievementsWhenSupported()
        {
            // Arrange
            Assume.That(!string.IsNullOrWhiteSpace(_refreshToken) || !string.IsNullOrWhiteSpace(_accessToken),
                "GOG credentials are not set in user secrets.");

            var client = new GogClient(_refreshToken, _accessToken);

            // Prefer a game with recorded playtime (more likely to have GOG Galaxy SDK achievements), or fallback to first owned game
            var playtimeMap = await client.FetchPlaytimeAsync();
            var games = await client.FetchOwnedGamesAsync();
            Assume.That(games, Is.Not.Empty, "No owned games found on GOG.");

            long targetGameId = playtimeMap.Keys.FirstOrDefault();
            if (targetGameId == 0)
            {
                targetGameId = games.First().Id;
            }

            // Act
            var result = await client.FetchGameAchievementsAsync(targetGameId);

            // Assert
            // Note: result can be null if the selected game does not integrate GOG Galaxy achievements
            Log.Information("Checked GOG achievements for Game ID {GameId}. Supported: {Supported}",
                targetGameId, result != null);

            if (result != null)
            {
                result.TotalAchievements.Should().BeGreaterThan(0);
                result.Achievements.Should().NotBeEmpty();
                Log.Information("Unlocked {Unlocked}/{Total} GOG achievements.",
                    result.UnlockedAchievements, result.TotalAchievements);
            }
        }

        [Test]
        public void GetLocallyInstalledGameIds_ShouldScanWindowsRegistryWithoutThrowing()
        {
            // Arrange
            var client = new GogClient();

            // Act
            var installedIds = client.GetLocallyInstalledGameIds();

            // Assert
            installedIds.Should().NotBeNull();
            Log.Information("Detected {Count} locally installed GOG games in Registry.", installedIds.Count);
        }
    }
}
