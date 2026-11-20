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
    [Category("Integration")]
    public class UbisoftIntegrationTests
    {
        private IConfiguration _config;
        private string? _rememberMeTicket;
        private string? _ticket;

        [OneTimeSetUp]
        public void SetupOnce()
        {
            _config = new ConfigurationBuilder()
                .AddUserSecrets<UbisoftIntegrationTests>()
                .Build();

            // Read Ubisoft Connect session tickets from user secrets
            _rememberMeTicket = _config["ubisoft_remember_me_ticket"];
            _ticket = _config["ubisoft_ticket"];
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

        // =========================================================================
        // ONE-TIME AUTH HELPER TESTS (Marked [Ignore] so fixture runs skip them)
        // =========================================================================

        [Test]
        [Ignore("Manual helper: run individually to sign in with email/password and print secrets.json tokens.")]
        public async Task Step1_LoginWithEmailAndPassword()
        {
            // Fill temporarily to generate tokens, then clear before committing:
            string email = "hed_pe@netcabo.pt";
            string password = "4E56FuO6";

            Assume.That(!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password),
                "Provide Ubisoft email and password temporarily to generate session tickets.");

            var client = new UbisoftClient();
            var session = await client.LoginWithCredentialsAsync(email, password);

            session.Should().NotBeNull("Ubisoft login should return a session or 2FA challenge.");

            if (!string.IsNullOrWhiteSpace(session!.TwoFactorAuthenticationTicket))
            {
                TestContext.Out.WriteLine("2FA is enabled on this account. Copy this ticket into Step2_CompleteTwoFactorLogin:");
                TestContext.Out.WriteLine($"TwoFactorAuthenticationTicket: {session.TwoFactorAuthenticationTicket}");
                return;
            }

            TestContext.Out.WriteLine("Add these keys to your secrets.json:");
            TestContext.Out.WriteLine($"\"ubisoft_remember_me_ticket\": \"{session.RememberMeTicket}\",");
            TestContext.Out.WriteLine($"\"ubisoft_ticket\": \"{session.Ticket}\"");
        }

        [Test]
        //[Ignore("Manual helper: run individually after copying the 'sessions' JSON response from ://ubisoft.com DevTools.")]
        public async Task Step1_ImportSessionJson()
        {
            // 1. Open UbisoftClient.LoginUrl in your browser and sign in.
            // 2. Open DevTools (F12) -> Network -> filter 'sessions' -> copy the JSON response body here:
            string sessionJson = @"{""rememberMeTicket"":""ewogICJ0eXAiOiAiSldFIiwKICAiZW5jIjogIkExMjhDQkMiLAogICJpdiI6ICJyelo0WkJQMm1GcWhBVU92OFlKYmx3IiwKICAiemlwIjogIkRFRiIsCiAgImludCI6ICJIUzI1NiIsCiAgImtpZCI6ICIwMDAwMDAwMC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAwMDAiCn0.HxBynOEYC-Z71Z-jPP6u5mU7Ts20WJ4YQPSIH5JU7j2NDcGvkGueEcKiDfiPDUcKkPVsbFaUpWTsVnZGbfl3YNB-fnDzFLb0zzMHao6ykIkLnsS8Uyz0wrWsH_uiACmw-oea4DV1FY2GYiKNki2JsEUxjX36A0nigNL_plzuYxGp8l5BP2UStHx6oRJ2zW49JdUp0mtb57byFETUxIObz1HV9nXuwMin487foNFZuRTpIm36_sq5yGOiKwHWj28-vnUpYCk4rRMWeRPuS-ukRgLOrkLwxc6SFL9dcog8gY56rTwiATxuui30nwT41He_OqYHBclOe7vi-_jyFt6WwHqEOCpgViFOfAOdCxU9ZbaZFH_iYGuXvC2kpm2VpYgoyeqG-8LZ1x2mCyRECv3zjEkhqflieSSVUmywFefDS4rARNmDJ6Rxrilr9WSTH27O0VvVc_iN-4qTnqQEQRvO7w.NnvTDyv3Kyt2iDI4udqVxS1UDPfbnEKiJJ2o9CbjQCI"",""expirationDate"":1797625823559,""email"":""hed_pe@netcabo.pt""}";

            Assume.That(!string.IsNullOrWhiteSpace(sessionJson),
                $"Open {UbisoftClient.LoginUrl}, sign in, and paste the 'sessions' JSON response into sessionJson.");

            var client = new UbisoftClient();
            var session = await client.ImportSessionFromJsonAsync(sessionJson);

            session.Should().NotBeNull("The pasted Ubisoft session JSON should contain a valid rememberMeTicket or ticket.");

            TestContext.Out.WriteLine("Add these keys to your secrets.json:");
            TestContext.Out.WriteLine($"\"ubisoft_remember_me_ticket\": \"{session!.RememberMeTicket}\",");
            TestContext.Out.WriteLine($"\"ubisoft_ticket\": \"{session.Ticket}\"");
        }

        [Test]
        [Ignore("Manual helper: run individually if Step 1 returned a TwoFactorAuthenticationTicket.")]
        public async Task Step2_CompleteTwoFactorLogin()
        {
            string twoFactorTicket = "";
            string twoFactorCode = "";

            Assume.That(!string.IsNullOrWhiteSpace(twoFactorTicket) && !string.IsNullOrWhiteSpace(twoFactorCode),
                "Provide the TwoFactorAuthenticationTicket from Step 1 and your 6-digit authenticator code.");

            var client = new UbisoftClient();
            var session = await client.CompleteTwoFactorLoginAsync(twoFactorTicket, twoFactorCode);

            session.Should().NotBeNull("2FA verification should return a valid Ubisoft session.");
            session!.Ticket.Should().NotBeNullOrWhiteSpace();

            TestContext.Out.WriteLine("Add these keys to your secrets.json:");
            TestContext.Out.WriteLine($"\"ubisoft_remember_me_ticket\": \"{session.RememberMeTicket}\",");
            TestContext.Out.WriteLine($"\"ubisoft_ticket\": \"{session.Ticket}\"");
        }

        // =========================================================================
        // AUTOMATED INTEGRATION TESTS
        // =========================================================================

        [Test]
        public async Task VerifySessionAsync_ShouldRefreshAndReturnUserProfile()
        {
            // Arrange
            Assume.That(!string.IsNullOrWhiteSpace(_rememberMeTicket),
                "ubisoft_remember_me_ticket is not set in user secrets.");

            var client = new UbisoftClient(_rememberMeTicket, _ticket);

            // Act
            var session = await client.VerifySessionAsync();

            // Assert
            session.Should().NotBeNull("VerifySessionAsync should succeed with a valid rememberMeTicket.");
            session!.UserId.Should().NotBeNullOrWhiteSpace();
            TestContext.Out.WriteLine($"Authenticated Ubisoft User: {session.NameOnPlatform} (UserId: {session.UserId})");
        }

        [Test]
        public async Task FetchOwnedGamesAsync_ShouldReturnOwnedPcGames()
        {
            // Arrange
            Assume.That(!string.IsNullOrWhiteSpace(_rememberMeTicket),
                "ubisoft_remember_me_ticket is not set in user secrets.");

            var client = new UbisoftClient(_rememberMeTicket, _ticket);

            // Act
              var games = await client.FetchOwnedGamesAsync();

            // Assert
            games.Should().NotBeNull();
            games.Should().NotBeEmpty("Your account should have at least one PC game on Ubisoft Connect.");

            var firstGame = games.First();
            firstGame.SpaceId.Should().NotBeNullOrWhiteSpace();
            firstGame.Title.Should().NotBeNullOrWhiteSpace();
            TestContext.Out.WriteLine($"Fetched {games.Count} PC games from Ubisoft Connect. Sample: {firstGame.Title} (SpaceId: {firstGame.SpaceId})");
        }

        [Test]
        public async Task FetchPlaytimeAsync_ShouldReturnPlaytimeDictionary()
        {
            // Arrange
            Assume.That(!string.IsNullOrWhiteSpace(_rememberMeTicket),
                "ubisoft_remember_me_ticket is not set in user secrets.");

            var client = new UbisoftClient(_rememberMeTicket, _ticket);

            // Act
            var playtimeMap = await client.FetchPlaytimeAsync();

            // Assert
            playtimeMap.Should().NotBeNull();
            TestContext.Out.WriteLine($"Retrieved playtime records for {playtimeMap.Count} Ubisoft games.");

            if (playtimeMap.Any())
            {
                var topPlayed = playtimeMap.OrderByDescending(kvp => kvp.Value).First();
                TestContext.Out.WriteLine($"Most played Ubisoft SpaceId: {topPlayed.Key} ({topPlayed.Value} mins)");
            }
        }

        [Test]
        public async Task FetchGameAchievementsAsync_ShouldReturnChallengesWhenSupported()
        {
            // Arrange
            Assume.That(!string.IsNullOrWhiteSpace(_rememberMeTicket),
                "ubisoft_remember_me_ticket is not set in user secrets.");

            var client = new UbisoftClient(_rememberMeTicket, _ticket);
            var games = await client.FetchOwnedGamesAsync();
            Assume.That(games, Is.Not.Empty, "No owned PC games found on Ubisoft Connect.");

            var sampleGame = games.OrderByDescending(g => g.PlaytimeSeconds).First();

            // Act
            var result = await client.FetchGameAchievementsAsync(sampleGame.SpaceId);

            // Assert
            TestContext.Out.WriteLine($"Checked Ubisoft challenges for {sampleGame.Title} (SpaceId: {sampleGame.SpaceId}). Supported: {result != null}");

            if (result != null)
            {
                result.TotalAchievements.Should().BeGreaterThan(0);
                result.Achievements.Should().NotBeEmpty();
                TestContext.Out.WriteLine($"Completed {result.UnlockedAchievements}/{result.TotalAchievements} Ubisoft challenges ({result.EarnedXp}/{result.TotalXp} XP).");
            }
        }

        [Test]
        public void GetLocallyInstalledGameIds_ShouldScanWindowsRegistryWithoutThrowing()
        {
            // Arrange
            var client = new UbisoftClient();

            // Act
            var installedIds = client.GetLocallyInstalledGameIds();

            // Assert
            installedIds.Should().NotBeNull();
            TestContext.Out.WriteLine($"Detected {installedIds.Count} locally installed Ubisoft Connect games in Registry.");
        }
    }
}
