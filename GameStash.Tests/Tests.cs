using FluentAssertions;
using GameStash.Core.Models;
using GameStash.Core.Services;
using GameStash.Tests.Config;
using Google.Apis.Auth.OAuth2;
using IgdbSharp.Models;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using NUnit.Framework.Internal;
using Serilog;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace GameStash.Tests
{
    [TestFixture, Order(1)]
    public class Tests
    {
        private IConfiguration _config;
        private string igdb_client_id;
        private string igdb_secret;

        [OneTimeSetUp]
        public void SetupOnce()
        {
            // AddUserSecrets<T>() automatically reads the secrets linked to this test assembly
            _config = new ConfigurationBuilder()
                .AddUserSecrets<Tests>()
                .Build();

            // Read the exact keys you defined in secrets.json
            igdb_client_id = _config["igdb_client_id"];
            igdb_secret = _config["igdb_secret"];
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
        public async Task Test_FileVersionInfo_GetFileMetadata()
        {
            string filePath = @"F:\Jogos\PC\Sins.of.a.Solar.Empire.Rebellion.Ultimate.Edition.v1.975.1.REPACK-KaOs\Install.exe";

            var fileInfo = FileVersionInfo.GetVersionInfo(filePath);
        }

        [Test]
        [DependsOnTest(nameof(Test_FileVersionInfo_GetFileMetadata))]
        public void Test_ScanFolder()
        {
            string folderPath = @"F:\Jogos\PC";

            var result = new GameScanner().ScanFolder(folderPath);

            // Assert: FluentAssertions validation framework matrix
            result.Should().NotBeNull();
            result.Should().NotBeEmpty("because the target PC games directory contains active installations");

            // Writes down the final summary report explicitly into the Test runner container
            TestContext.Out.WriteLine("==========================================================================");
            TestContext.Out.WriteLine($"  SCAN FINISHED - FOUND {result.Count()} FILES TO PROCESS");
            TestContext.Out.WriteLine("==========================================================================");
            TestContext.Out.WriteLine(string.Join("\n", result));
        }

        [Test]
        [DependsOnTest(nameof(Test_ScanFolder))]
        public async Task Test_ScanFolderAsync()
        {
            string folderPath = @"F:\Jogos\PC";

            var result = await new GameScanner().ScanFolderAsync(folderPath);

            // Assert: FluentAssertions validation framework matrix
            result.Should().NotBeNull();
            result.Should().NotBeEmpty("because the target PC games directory contains active installations");

            // Writes down the final summary report explicitly into the Test runner container
            TestContext.Out.WriteLine("==========================================================================");
            TestContext.Out.WriteLine($"  SCAN FINISHED - FOUND {result.Count()} FILES TO PROCESS");
            TestContext.Out.WriteLine("==========================================================================");
            TestContext.Out.WriteLine(string.Join("\n", result));
        }

        [Test]
        [DependsOnTest(nameof(Test_ScanFolderAsync))]
        public void Test_ScanFolderAndIdentifyGames()
        {
            string folderPath = @"F:\Jogos\PC";

            var result = new GameScanner().ScanFolder(folderPath);

            result.Should().NotBeNull();
            result.Should().NotBeEmpty("because the target PC games directory contains active installations");
            
            // Writes down the final summary report explicitly into the Test runner container
            TestContext.Out.WriteLine("==========================================================================");
            TestContext.Out.WriteLine($"  SCAN FINISHED - FOUND {result.Count()} FILES TO PROCESS");
            TestContext.Out.WriteLine("==========================================================================");

            var games = new GameIdentifier().IdentifyGames(result);

            games.Should().NotBeNull();
            games.Should().NotBeEmpty("because the target PC games directory contains active installations");

            // Writes down the final summary report explicitly into the Test runner container
            TestContext.Out.WriteLine("==========================================================================");
            TestContext.Out.WriteLine($"  IDENTIFICATION FINISHED - FOUND {games.Count()} FILES TO PROCESS");
            TestContext.Out.WriteLine("==========================================================================");
            TestContext.Out.WriteLine(string.Join("\n", games.Select(game => $"{game.PackageType} - {game.MatchName}")));
        }

        [Test]
        [DependsOnTest(nameof(Test_ScanFolderAndIdentifyGames))]
        public async Task Test_ScanFolderAndIdentifyGamesAsync()
        {
            string folderPath = @"F:\Jogos\PC";

            var result = await new GameScanner().ScanFolderAsync(folderPath);

            result.Should().NotBeNull();
            result.Should().NotBeEmpty("because the target PC games directory contains active installations");

            // Writes down the final summary report explicitly into the Test runner container
            TestContext.Out.WriteLine("==========================================================================");
            TestContext.Out.WriteLine($"  SCAN FINISHED - FOUND {result.Count()} FILES TO PROCESS");
            TestContext.Out.WriteLine("==========================================================================");

            var games = await new GameIdentifier().IdentifyGamesAsync(result);

            games.Should().NotBeNull();
            games.Should().NotBeEmpty("because the target PC games directory contains active installations");

            // Writes down the final summary report explicitly into the Test runner container
            TestContext.Out.WriteLine("==========================================================================");
            TestContext.Out.WriteLine($"  IDENTIFICATION FINISHED - FOUND {games.Count()} FILES TO PROCESS");
            TestContext.Out.WriteLine("==========================================================================");
            TestContext.Out.WriteLine(string.Join("\n", games.Select(game => $"{game.PackageType} - {game.MatchName}")));
        }

        [Test]
        [DependsOnTest(nameof(Test_ScanFolderAndIdentifyGamesAsync))]
        public async Task Test_ScanFolderIdentifyGamesAndMatchGamesAsync()
        {
            string folderPath = @"F:\Jogos\PC";

            var result = await new GameScanner().ScanFolderAsync(folderPath);

            result.Should().NotBeNull();
            result.Should().NotBeEmpty("because the target PC games directory contains active installations");

            // Writes down the final summary report explicitly into the Test runner container
            TestContext.Out.WriteLine("==========================================================================");
            TestContext.Out.WriteLine($"  SCAN FINISHED - FOUND {result.Count()} FILES TO PROCESS");
            TestContext.Out.WriteLine("==========================================================================");

            var games = await new GameIdentifier().IdentifyGamesAsync(result);

            games.Should().NotBeNull();
            games.Should().NotBeEmpty("because the target PC games directory contains active installations");

            // Writes down the final summary report explicitly into the Test runner container
            TestContext.Out.WriteLine("==========================================================================");
            TestContext.Out.WriteLine($"  IDENTIFICATION FINISHED - IDENTIFIED {games.Count()} GAMES");
            TestContext.Out.WriteLine("==========================================================================");
            TestContext.Out.WriteLine(string.Join("\n", games.Select(game => $"{game.PackageType} - {game.MatchName}")));

            await Parallel.ForEachAsync(games, CancellationToken.None/*new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token*/,
                async (game, token) => await game.MatchIgdbGame(igdb_client_id, igdb_secret));

            Parallel.ForEach(games, game =>
            {
                if (game.IgdbSelectedMatch != null)
                    game.MatchIgdbGame(game.IgdbSelectedMatch);
            });

            // Writes down the final summary report explicitly into the Test runner container
            TestContext.Out.WriteLine("===========================================================================");
            TestContext.Out.WriteLine($"  GAME MATCHING FINISHED - MATCHED {games.Count(g => g.Id.HasValue || g.IgdbMatches?.Any() == true)} OUT OF {games.Count()}");
            TestContext.Out.WriteLine("===========================================================================");

            TestContext.Out.WriteLine(string.Join("\n", games.Select(game => game.Id.HasValue || game.IgdbMatches?.Any() == true ? $"MATCHED - {(game.Id.HasValue ? game.Name : "FOUND " + game.IgdbMatches.Count() + " MATCHES FOR " + game.MatchName)}{(!string.IsNullOrEmpty(game.IgdbSelectedMatch?.Name) ? " - " + game.IgdbSelectedMatch?.Name : "")}" : $"NOT MATCHED - {game.MatchName}")));
        }

        [Test]
        [DependsOnTest(nameof(Test_ScanFolderIdentifyGamesAndMatchGamesAsync))]
        public async Task Test_IGDBGame()
        {
            int game_id = 140427;

            var game = new Core.Models.StashGame { Id = game_id };

            game.Should().NotBeNull();
            game.Id.Should().HaveValue("because the identity allocation phase should be completed");
            game.Id.Should().BeGreaterThanOrEqualTo(game_id, "because a valid game entry must have a populated identifier");

            await game.Refresh(igdb_client_id, igdb_secret);
            game.MatchIgdbGame(game.IgdbSelectedMatch);

            TestContext.Out.WriteLine(!string.IsNullOrEmpty(game.Name) ? $"MATCHED - {game.Id} - {game.Name}" : $"NOT MATCHED - {game.MatchName}");
        }
    }
}