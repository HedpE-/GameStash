using FluentAssertions;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace GameStash.Tests
{
    [TestFixture]
    [Category("Integration")]
    public class AiTests
    {
        public class GameSuggestion
        {
            public string Title { get; set; } = string.Empty;
            public string ReasonToPlay { get; set; } = string.Empty;
            public string MatchPercentage { get; set; } = string.Empty;
        }

        public class SuggestionResponse
        {
            public GameSuggestion[] Suggestions { get; set; } = [];
        }

        private IConfiguration _config;

        [OneTimeSetUp]
        public void SetupOnce()
        {
            // AddUserSecrets<T>() automatically reads the secrets linked to this test assembly
            _config = new ConfigurationBuilder()
                .AddUserSecrets<AiTests>()
                .Build();
        }

        [Test]
        public async Task GenerateGameSuggestions_ShouldReturnStructuredRecommendations()
        {
            // Arrange
            var apiKey = _config["GEMINI_API_KEY"];
            Assume.That(!string.IsNullOrWhiteSpace(apiKey), "GEMINI_API_KEY is not configured in user secrets.");

            var client = new Client(apiKey: apiKey);
            const string model = "gemini-2.5-flash";

            var userLibraryJson = @"[
                { ""title"": ""The Witcher 3"", ""hoursPlayed"": 120, ""status"": ""Completed"" },
                { ""title"": ""Cyberpunk 2077"", ""hoursPlayed"": 85, ""status"": ""Completed"" },
                { ""title"": ""Elden Ring"", ""hoursPlayed"": 10, ""status"": ""Backlog"" },
                { ""title"": ""Stardew Valley"", ""hoursPlayed"": 4, ""status"": ""Abandoned"" }
            ]";

            var systemInstruction = "You are a specialized video game assistant integrated into a native game launcher.";
            var prompt = $"Based on the user's local library below, suggest 2 new games on the market to buy and 1 game from their current Backlog that they should give a second chance:\n{userLibraryJson}";

            var gameSuggestionSchema = new Schema
            {
                Type = Google.GenAI.Types.Type.Object,
                Properties = new Dictionary<string, Schema>
                {
                    { "Title", new Schema { Type = Google.GenAI.Types.Type.String } },
                    { "ReasonToPlay", new Schema { Type = Google.GenAI.Types.Type.String } },
                    { "MatchPercentage", new Schema { Type = Google.GenAI.Types.Type.String } }
                },
                Required = new List<string> { "Title", "ReasonToPlay", "MatchPercentage" }
            };

            var responseSchema = new Schema
            {
                Type = Google.GenAI.Types.Type.Object,
                Properties = new Dictionary<string, Schema>
                {
                    { "Suggestions", new Schema { Type = Google.GenAI.Types.Type.Array, Items = gameSuggestionSchema } }
                },
                Required = new List<string> { "Suggestions" }
            };

            var config = new GenerateContentConfig
            {
                SystemInstruction = new Content
                {
                    Parts = [new Part { Text = systemInstruction }]
                },
                ResponseMimeType = "application/json",
                ResponseSchema = responseSchema
            };

            // Act
            var response = await client.Models.GenerateContentAsync(
                model,
                new Content { Parts = [new Part { Text = prompt }] },
                config
            );

            // Assert
            response.Should().NotBeNull();
            response.Text.Should().NotBeNullOrWhiteSpace();

            var gameData = JsonSerializer.Deserialize<SuggestionResponse>(response.Text);
            gameData.Should().NotBeNull();
            gameData!.Suggestions.Should().HaveCount(3);

            foreach (var game in gameData.Suggestions)
            {
                game.Title.Should().NotBeNullOrWhiteSpace();
                game.ReasonToPlay.Should().NotBeNullOrWhiteSpace();
                game.MatchPercentage.Should().NotBeNullOrWhiteSpace();

                TestContext.Out.WriteLine($"\n🎮 Game: {game.Title}");
                TestContext.Out.WriteLine($"🔥 Match: {game.MatchPercentage}");
                TestContext.Out.WriteLine($"💡 Why play: {game.ReasonToPlay}");
            }
        }
    }
}
