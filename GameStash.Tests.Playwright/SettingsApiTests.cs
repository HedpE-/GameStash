using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using GameStash.Tests.Playwright.Setup;
using NUnit.Framework;

namespace GameStash.Tests.Playwright.ApiTests
{
    public class SettingsApiTests : ApiTestBase
    {
        [Test]
        public async Task GetSettings_ShouldReturnOkWithConfigurationStructure()
        {
            // Act: Request global settings payload
            var response = await ApiRequest.GetAsync("/api/settings");

            // Assert
            response.Status.Should().Be(200);

            var json = await response.TextAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Validate that expected configuration keys are present
            root.TryGetProperty("igdbClientId", out _).Should().BeTrue();
            root.TryGetProperty("igdbSecret", out _).Should().BeTrue();
        }

        [Test]
        public async Task GetStatus_WithInvalidProvider_ShouldReturnBadRequest()
        {
            // Act: Request status for an unknown API provider
            var response = await ApiRequest.GetAsync("/api/settings/invalid_provider/status");

            // Assert
            response.Status.Should().Be(400);

            var json = await response.TextAsync();
            json.Should().Contain("Unsupported credentials type");
        }

        [Test]
        public async Task UpdateCredentials_WithEmptyPayload_ShouldReturnBadRequest()
        {
            // Act: Send PUT with empty credentials
            var response = await ApiRequest.PutAsync("/api/settings/credentials/igdb", new()
            {
                DataObject = new
                {
                    UserClientId = "",
                    Secret = ""
                }
            });

            // Assert
            response.Status.Should().Be(400);
        }
    }
}