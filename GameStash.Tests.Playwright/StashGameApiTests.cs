using System.Threading.Tasks;
using FluentAssertions;
using GameStash.Tests.Playwright.Setup;
using NUnit.Framework;

namespace GameStash.Tests.Playwright.ApiTests
{
    public class StashGameApiTests : ApiTestBase
    {
        [Test]
        public async Task UpdateGameDetails_OnNonExistentGame_ShouldReturnNotFound()
        {
            const long nonExistentGameId = 999999999;

            // Act: Attempt to update a property on a game that does not exist
            var response = await ApiRequest.PutAsync($"/api/stash/local/{nonExistentGameId}/details", new()
            {
                DataObject = new
                {
                    PropertyName = "IsFavorite",
                    Value = true
                }
            });

            // Assert
            response.Status.Should().Be(404);
        }

        [Test]
        public async Task GetSameFranchiseGames_WithoutFranchise_ShouldReturnEmptyCollection()
        {
            // Act: Query franchise endpoint without passing franchise parameter
            var response = await ApiRequest.GetAsync("/api/games/franchise");

            // Assert: Endpoint should handle null safely and return empty array
            response.Status.Should().Be(200);

            var json = await response.TextAsync();
            json.Should().Be("[]");
        }
    }
}