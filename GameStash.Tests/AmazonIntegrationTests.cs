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
    public class AmazonIntegrationTests
    {
        private IConfiguration _config;
        private string? _refreshToken;
        private string? _accessToken;

        [OneTimeSetUp]
        public void SetupOnce()
        {
            _config = new ConfigurationBuilder()
                .AddUserSecrets<AmazonIntegrationTests>()
                .Build();

            // Read Amazon credentials from user secrets
            _refreshToken = _config["amazon_refresh_token"];
            _accessToken = _config["amazon_access_token"];
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
        // ONE-TIME AUTH HELPER TESTS (Run manually to generate secrets.json tokens)
        // =========================================================================

        [Test]
        [Explicit("Run manually to generate the Amazon OAuth login URL and PKCE parameters.")]
        [Ignore("Manual helper: run individually only when generating a new Amazon OAuth login URL.")]
        public void Step1_PrintAmazonLoginUrl()
        {
            var (loginUrl, codeVerifier, deviceSerial) = AmazonClient.GenerateLoginUrl();

            TestContext.Out.WriteLine("1. Open this URL in your browser and sign in:");
            TestContext.Out.WriteLine(loginUrl);
            TestContext.Out.WriteLine("\n2. Save these values for Step 2:");
            TestContext.Out.WriteLine($"CodeVerifier: {codeVerifier}");
            TestContext.Out.WriteLine($"DeviceSerial: {deviceSerial}");
        }

        [Test]
        [Explicit("Paste the values from Step 1 and the redirected browser URL, then run manually.")]
        [Ignore("Manual helper: run individually after pasting the redirected browser URL from Step 1.")]
        public async Task Step2_ExchangeCodeForTokens()
        {
            // Paste values here:
            string redirectedUrlOrCode = "https://www.amazon.com/ap/maplanding?openid.assoc_handle=amzn_sonic_games_launcher&openid.claimed_id=https%3A%2F%2Fwww.amazon.com%2Fap%2Fid%2Famzn1.account.AF572NRDEGNXCX4BUCZ6XAEB2KRQ&openid.identity=https%3A%2F%2Fwww.amazon.com%2Fap%2Fid%2Famzn1.account.AF572NRDEGNXCX4BUCZ6XAEB2KRQ&openid.mode=id_res&openid.ns=http%3A%2F%2Fspecs.openid.net%2Fauth%2F2.0&openid.op_endpoint=https%3A%2F%2Fwww.amazon.com%2Fap%2Fsignin&openid.response_nonce=2026-11-19T14%3A05%3A07Z-5981209739840569965&openid.return_to=https%3A%2F%2Fwww.amazon.com%2Fap%2Fmaplanding&openid.signed=assoc_handle%2Cclaimed_id%2Cidentity%2Cmode%2Cns%2Cop_endpoint%2Cresponse_nonce%2Creturn_to%2Cns.pape%2Cpape.auth_policies%2Cpape.auth_time%2Coa2.authorization_code%2Csigned&openid.ns.pape=http%3A%2F%2Fspecs.openid.net%2Fextensions%2Fpape%2F1.0&openid.pape.auth_policies=http%3A%2F%2Fschemas.openid.net%2Fpape%2Fpolicies%2F2007%2F06%2Fnone&openid.pape.auth_time=2026-11-19T14%3A05%3A07Z&openid.sig=ui%2B4BRu%2BRGdcReDnvaPbDMjmO9Slqe%2BM5Y3jYtJ%2BdTU%3D&serial=&openid.oa2.authorization_code=ANuETgHyWIizASgxIppSxMIS&openid.ns.oa2=http%3A%2F%2Fwww.amazon.com%2Fap%2Fext%2Foauth%2F2";
            string codeVerifier = "wWhs7YG_RXU4XvrO6jASXxxFmrInPECheiLN6l70PAQ";
            string deviceSerial = "65B32012185D402ABB9A681AC24E7B90";

            Assume.That(!string.IsNullOrWhiteSpace(redirectedUrlOrCode), "Paste the redirected URL from your browser first.");

            string? capturedRefresh = null;
            string? capturedAccess = null;

            var client = new AmazonClient(
                onTokensRefreshed: (refresh, access) =>
                {
                    capturedRefresh = refresh;
                    capturedAccess = access;
                    return Task.CompletedTask;
                });

            bool success = await client.ExchangeCodeForTokenAsync(redirectedUrlOrCode, codeVerifier, deviceSerial);
            success.Should().BeTrue("Token exchange should succeed with valid authorization code and PKCE verifier.");

            TestContext.Out.WriteLine("Add these keys to your secrets.json:");
            TestContext.Out.WriteLine($"\"amazon_refresh_token\": \"{capturedRefresh}\",");
            TestContext.Out.WriteLine($"\"amazon_access_token\": \"{capturedAccess}\"");
        }

        // =========================================================================
        // AUTOMATED INTEGRATION TESTS
        // =========================================================================

        [Test]
        public async Task GetUserProfileAsync_ShouldValidateAndReturnAmazonProfile()
        {
            // Arrange
            Assume.That(!string.IsNullOrWhiteSpace(_refreshToken) || !string.IsNullOrWhiteSpace(_accessToken),
                "Amazon credentials are not set in user secrets.");

            var client = new AmazonClient(_refreshToken, _accessToken);

            // Act
            var profile = await client.GetUserProfileAsync();

            // Assert
            profile.Should().NotBeNull("GetUserProfileAsync should succeed with a valid or refreshable token.");
            profile!.UserId.Should().NotBeNullOrWhiteSpace();
            TestContext.Out.WriteLine($"Authenticated Amazon User: {profile.Name} (UserId: {profile.UserId})");
        }

        [Test]
        public async Task FetchOwnedGamesAsync_ShouldReturnOwnedEntitlements()
        {
            // Arrange
            Assume.That(!string.IsNullOrWhiteSpace(_refreshToken) || !string.IsNullOrWhiteSpace(_accessToken),
                "Amazon credentials are not set in user secrets.");

            var client = new AmazonClient(_refreshToken, _accessToken);

            // Act
            var entitlements = await client.FetchOwnedGamesAsync();

            // Assert
            entitlements.Should().NotBeNull();
            entitlements.Should().NotBeEmpty("Your account should have at least one Prime Gaming / Amazon Games entitlement.");

            var firstGame = entitlements.First().Product!;
            firstGame.Id.Should().NotBeNullOrWhiteSpace();
            firstGame.Title.Should().NotBeNullOrWhiteSpace();
            TestContext.Out.WriteLine($"Fetched {entitlements.Count} entitlements from Amazon Games. Sample: {firstGame.Title} (ID: {firstGame.Id})");
        }

        [Test]
        public async Task FetchGameAchievementsAsync_ShouldNotThrowWhenCalled()
        {
            // Arrange
            Assume.That(!string.IsNullOrWhiteSpace(_refreshToken) || !string.IsNullOrWhiteSpace(_accessToken),
                "Amazon credentials are not set in user secrets.");

            var client = new AmazonClient(_refreshToken, _accessToken);
            var entitlements = await client.FetchOwnedGamesAsync();
            Assume.That(entitlements, Is.Not.Empty, "No owned games found on Amazon Games.");

            var sampleProduct = entitlements.First().Product!;

            // Act
            var result = await client.FetchGameAchievementsAsync(sampleProduct.Id);

            // Assert
            // Standard Prime Gaming titles return null; Amazon Luna titles return achievement records
            TestContext.Out.WriteLine($"Checked achievements for {sampleProduct.Title} (ProductId: {sampleProduct.Id}). Supported: {result != null}");

            if (result != null)
            {
                result.TotalAchievements.Should().BeGreaterThan(0);
                result.Achievements.Should().NotBeEmpty();
            }
        }

        [Test]
        public void GetLocallyInstalledGameIds_ShouldScanLocalManifestsWithoutThrowing()
        {
            // Arrange
            var client = new AmazonClient();

            // Act
            var installedIds = client.GetLocallyInstalledGameIds();

            // Assert
            installedIds.Should().NotBeNull();
            TestContext.Out.WriteLine($"Detected {installedIds.Count} locally installed Amazon Games.");
        }
    }
}
