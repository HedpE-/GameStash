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
    public class SteamIntegrationTests
    {
        private IConfiguration _config;
        private string? _apiKey;
        private string? _steamId;

        [OneTimeSetUp]
        public void SetupOnce()
        {
            _config = new ConfigurationBuilder()
                .AddUserSecrets<SteamIntegrationTests>()
                .Build();

            // Read the exact keys defined in secrets.json
            _steamId = _config["steam_user_id_64"];
            _apiKey = _config["steam_api_key"];
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
        public async Task GetAuthenticationStatusAsync_ShouldReturnTrueForValidCredentials()
        {
            // Arrange
            Assume.That(!string.IsNullOrWhiteSpace(_apiKey), "steam_api_key is not set in user secrets.");
            Assume.That(!string.IsNullOrWhiteSpace(_steamId), "steam_user_id_64 is not set in user secrets.");

            var client = new SteamClient(_steamId!, _apiKey!);

            // Act
            var isAuthenticated = await client.GetAuthenticationStatusAsync();

            // Assert
            isAuthenticated.Should().BeTrue();
        }

        [Test]
        public async Task FetchOwnedGamesAsync_ShouldReturnRealDataFromSteamApi()
        {
            // Arrange
            Assume.That(!string.IsNullOrWhiteSpace(_apiKey), "steam_api_key is not set in user secrets.");
            Assume.That(!string.IsNullOrWhiteSpace(_steamId), "steam_user_id_64 is not set in user secrets.");

            var client = new SteamClient(_steamId!, _apiKey!);

            // Act
            var games = await client.FetchOwnedGamesAsync();

            // Assert
            games.Should().NotBeNull();
            games.Should().NotBeEmpty("Your account should have at least one game on Steam.");

            var firstGame = games.First();
            firstGame.Name.Should().NotBeNullOrWhiteSpace();
            firstGame.AppId.Should().BeGreaterThan(0);
            Log.Information("Fetched {Count} games from Steam Web API. Sample: {Name} (AppId: {AppId})",
                games.Count, firstGame.Name, firstGame.AppId);
        }

        [Test]
        public async Task FetchGameAchievementsAsync_ShouldReturnAchievementsSchemaAndRarity()
        {
            // Arrange
            Assume.That(!string.IsNullOrWhiteSpace(_apiKey), "steam_api_key is not set in user secrets.");
            Assume.That(!string.IsNullOrWhiteSpace(_steamId), "steam_user_id_64 is not set in user secrets.");

            var client = new SteamClient(_steamId!, _apiKey!);

            // Pick the user's most played owned game to maximize the chance of it having Steam achievements
            var games = await client.FetchOwnedGamesAsync();
            Assume.That(games, Is.Not.Empty, "No owned games found on Steam.");

            var mostPlayedGame = games
                .OrderByDescending(g => g.PlaytimeForeverMinutes)
                .First();

            // Act
            var result = await client.FetchGameAchievementsAsync(mostPlayedGame.AppId);

            // Assert
            Log.Information("Checked Steam achievements for {Name} (AppId: {AppId}). Supported: {Supported}",
                mostPlayedGame.Name, mostPlayedGame.AppId, result != null);

            if (result != null)
            {
                result.TotalAchievements.Should().BeGreaterThan(0);
                result.Achievements.Should().NotBeEmpty();

                var sampleAchievement = result.Achievements.First();
                sampleAchievement.Id.Should().NotBeNullOrWhiteSpace();
                sampleAchievement.Name.Should().NotBeNullOrWhiteSpace();
                sampleAchievement.DisplayIconUrl.Should().NotBeNullOrWhiteSpace();

                Log.Information("Unlocked {Unlocked}/{Total} Steam achievements for {Name}.",
                    result.UnlockedAchievements, result.TotalAchievements, result.GameName);
            }
        }

        [Test]
        public void GetLocallyInstalledAppIds_ShouldReturnActualInstalledApps()
        {
            // Arrange
            var client = new SteamClient("dummy", "dummy");

            // Act
            var installedApps = client.GetLocallyInstalledAppIds();

            // Assert
            installedApps.Should().NotBeNull();
            Log.Information("Detected {Count} locally installed Steam games.", installedApps.Count);
        }
    }
}
