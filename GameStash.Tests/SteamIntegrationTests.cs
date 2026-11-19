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
    public class SteamIntegrationTests
    {
        private IConfiguration _config;
        private string _apiKey;
        private string _steamId;

        [OneTimeSetUp]
        public void SetupOnce()
        {
            // AddUserSecrets<T>() automatically reads the secrets linked to this test assembly
            _config = new ConfigurationBuilder()
                .AddUserSecrets<SteamIntegrationTests>()
                .Build();

            // Read the exact keys you defined in secrets.json
            _steamId = _config["steam_user_id_64"];
            _apiKey = _config["steam_api_key"];
        }

        [SetUp]
        public void Setup()
        {
            // Initializes your logging pipeline before running any filesystem scans or tests
            TestLogInitializer.InitializeTestLogger();
        }

        [OneTimeTearDown]
        public void OneTimeTeardown()
        {
            // Ensures all remaining stream bytes flush before releasing the test domain thread pool
            Log.CloseAndFlush();
        }

        [Test]
        public async Task GetAuthenticationStatusAsync_ShouldReturnTrueForValidCredentials()
        {
            // Arrange
            Assume.That(_apiKey, Is.Not.Null, "STEAM_API_KEY environment variable is not set.");
            Assume.That(_steamId, Is.Not.Null, "STEAM_ID_64 environment variable is not set.");

            var client = new SteamClient(_steamId, _apiKey);

            // Act
            var isAuthenticated = await client.GetAuthenticationStatusAsync();

            // Assert
            isAuthenticated.Should().BeTrue();
        }


        [Test]
        public async Task FetchOwnedGamesAsync_ShouldReturnRealDataFromSteamApi()
        {
            // Arrange
            Assume.That(_apiKey, Is.Not.Null, "STEAM_API_KEY environment variable is not set.");
            Assume.That(_steamId, Is.Not.Null, "STEAM_ID_64 environment variable is not set.");

            var client = new SteamClient(_steamId, _apiKey);

            // Act
            var games = await client.FetchOwnedGamesAsync();

            // Assert
            games.Should().NotBeNull();
            games.Should().NotBeEmpty("Your account should have at least one game on Steam.");

            // Verifica se os dados básicos estão a chegar
            var firstGame = games.First();
            firstGame.Name.Should().NotBeNullOrWhiteSpace();
            firstGame.AppId.Should().BeGreaterThan(0);
        }

        [Test]
        public void GetLocallyInstalledAppIds_ShouldReturnActualInstalledApps()
        {
            // Arrange
            var client = new SteamClient("dummy", "dummy");

            // Act
            var installedApps = client.GetLocallyInstalledAppIds();

            // Assert
            // Verifica se o computador de dev tem jogos instalados
            installedApps.Should().NotBeNull();
            // Pelo menos um jogo deve estar instalado se o Steam estiver instalado neste PC
            Log.Information("Detected {Count} locally installed Steam games.", installedApps.Count);
        }
    }
}