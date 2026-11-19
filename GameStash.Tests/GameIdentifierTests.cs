using FluentAssertions;
using GameStash.Core.Services;
using GameStash.Tests.Config;
using NUnit.Framework;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace GameStash.Core.Tests
{
    [TestFixture]
    public class GameIdentifierTests
    {
        private string _tempRootDir;
        private GameIdentifier _identifier;

        [SetUp]
        public void Setup()
        {
            TestLogInitializer.InitializeTestLogger();
            _tempRootDir = Path.Combine(Path.GetTempPath(), "GameStash_IdentifierTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRootDir);
            _identifier = new GameIdentifier();
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_tempRootDir))
                Directory.Delete(_tempRootDir, recursive: true);

            Log.CloseAndFlush();
        }

        [Test]
        public async Task IdentifyGamesAsync_WhenGamestashJsonExists_ShouldReadMetadataDirectly()
        {
            // Arrange
            var gameDir = Path.Combine(_tempRootDir, "SomeObscureFolder");
            Directory.CreateDirectory(gameDir);
            var jsonPath = Path.Combine(gameDir, "gamestash.json");
            await File.WriteAllTextAsync(jsonPath, "{\"Name\":\"The Witcher 3: Wild Hunt\",\"Type\":\"GOG\"}");

            // Act
            var results = (await _identifier.IdentifyGamesAsync(new[] { jsonPath })).ToList();

            // Assert
            results.Should().ContainSingle();
            var game = results.Single();
            game.MatchName.Should().Be("The Witcher 3: Wild Hunt");
            game.PackageType.Should().Be("GOG");
            game.Folder.Should().Be(gameDir);
        }

        [Test]
        public async Task IdentifyGamesAsync_WhenGamestashJsonIsCorrupted_ShouldDeleteItAndFallbackToNormalDetection()
        {
            // Arrange
            var gameDir = Path.Combine(_tempRootDir, "MrDJ_Test");
            Directory.CreateDirectory(gameDir);

            // Corrupted JSON
            var jsonPath = Path.Combine(gameDir, "gamestash.json");
            await File.WriteAllTextAsync(jsonPath, "{ invalid json :::");

            // Mr DJ Repack signature files
            var setupPath = Path.Combine(gameDir, "Setup.exe");
            await File.WriteAllTextAsync(setupPath, "dummy");
            await File.WriteAllTextAsync(Path.Combine(gameDir, "Setup-1.bin"), "dummy");
            await File.WriteAllTextAsync(Path.Combine(gameDir, "autorun.inf"), "[autorun]");
            await File.WriteAllTextAsync(Path.Combine(gameDir, "Icon.ico"), "dummy");

            // Act
            var results = (await _identifier.IdentifyGamesAsync(new[] { setupPath })).ToList();

            // Assert
            results.Should().ContainSingle();
            var game = results.Single();
            game.PackageType.Should().Be("Mr DJ Repack");
            game.MatchName.Should().Be("MrDJ_Test");

            // Verify gamestash.json was rewritten with valid JSON
            File.Exists(jsonPath).Should().BeTrue();
            var rewritten = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(jsonPath));
            rewritten!["Name"].Should().Be("MrDJ_Test");
            rewritten["Type"].Should().Be("Mr DJ Repack");
        }

        [Test]
        public async Task IdentifyGamesAsync_ShouldDetectElAmigosRepackFromAutorunInf()
        {
            // Arrange
            var gameDir = Path.Combine(_tempRootDir, "ElAmigosFolder");
            Directory.CreateDirectory(gameDir);

            var setupPath = Path.Combine(gameDir, "Setup.exe");
            await File.WriteAllTextAsync(setupPath, "dummy");
            await File.WriteAllTextAsync(Path.Combine(gameDir, "elamigos-01.bin"), "dummy");
            await File.WriteAllTextAsync(Path.Combine(gameDir, "Icon.ico"), "dummy");
            await File.WriteAllTextAsync(Path.Combine(gameDir, "autorun.inf"), "[autorun]\r\nopen=Setup.exe\r\nlabel=Elden Ring Deluxe\r\n");

            // Act
            var results = (await _identifier.IdentifyGamesAsync(new[] { setupPath })).ToList();

            // Assert
            results.Should().ContainSingle();
            var game = results.Single();
            game.PackageType.Should().Be("El Amigos Repack");
            game.MatchName.Should().Be("Elden Ring Deluxe");
        }

        [Test]
        public async Task IdentifyGamesAsync_ShouldDetectKaOsRepackFromNfoFile()
        {
            // Arrange
            var gameDir = Path.Combine(_tempRootDir, "KaOsFolder");
            Directory.CreateDirectory(gameDir);

            var setupPath = Path.Combine(gameDir, "Install.exe");
            await File.WriteAllTextAsync(setupPath, "dummy");
            await File.WriteAllTextAsync(Path.Combine(gameDir, "KaOs.bin"), "dummy");
            await File.WriteAllTextAsync(Path.Combine(gameDir, "KaOs.nfo"), "RELEASE : Sins of a Solar Empire Rebellion REPACK-KaOs\r\n");

            // Act
            var results = (await _identifier.IdentifyGamesAsync(new[] { setupPath })).ToList();

            // Assert
            results.Should().ContainSingle();
            var game = results.Single();
            game.PackageType.Should().Be("KaOs Repack");
            game.MatchName.Should().Be("Sins of a Solar Empire Rebellion");
        }

        [Test]
        public async Task IdentifyGamesAsync_ShouldDetectKaOsRepackFromFolderNameWhenNfoIsMissing()
        {
            // Arrange
            var gameDir = Path.Combine(_tempRootDir, "Factorio.v1.1.100.REPACK-KaOs");
            Directory.CreateDirectory(gameDir);

            var setupPath = Path.Combine(gameDir, "Install.exe");
            await File.WriteAllTextAsync(setupPath, "dummy");
            await File.WriteAllTextAsync(Path.Combine(gameDir, "KaOs-data.bin"), "dummy");

            // Act
            var results = (await _identifier.IdentifyGamesAsync(new[] { setupPath })).ToList();

            // Assert
            results.Should().ContainSingle();
            var game = results.Single();
            game.PackageType.Should().Be("KaOs Repack");
            game.MatchName.Should().Be("Factorio v1 1 100");
        }

        [TestCase("fg-01.bin", "FitGirl Repack")]
        [TestCase("setup-fitgirl-1.bin", "FitGirl Repack")]
        [TestCase("data1.doi", "Dodi Repack")]
        [TestCase("data1.dd", "Dodi Repack")]
        [TestCase("data01.tr", "Tiny Repack")]
        [TestCase("data1.bin", "R.G. Mechanics")]
        public async Task IdentifyGamesAsync_WhenBinarySignatureMatchesWithoutPeMetadata_ShouldNotCreateEmptyEntry(
            string signatureFileName,
            string expectedRepackCategory)
        {
            // Arrange
            var gameDir = Path.Combine(_tempRootDir, $"RepackTest_{expectedRepackCategory.Replace(" ", "")}");
            Directory.CreateDirectory(gameDir);

            var setupPath = Path.Combine(gameDir, "setup.exe");
            await File.WriteAllTextAsync(setupPath, "dummy-without-pe-version-info");
            await File.WriteAllTextAsync(Path.Combine(gameDir, signatureFileName), "dummy-archive");

            // Act
            var results = (await _identifier.IdentifyGamesAsync(new[] { setupPath })).ToList();

            // Assert
            // Because the dummy setup.exe has no embedded ProductName/FileDescription, gameName remains empty
            // and the identifier should safely skip creating an invalid entry or blank gamestash.json
            results.Should().BeEmpty();
            File.Exists(Path.Combine(gameDir, "gamestash.json")).Should().BeFalse();
        }

        [Test]
        public async Task IdentifyGamesAsync_ShouldIgnoreGamestashIgnoreMarker()
        {
            // Act
            var results = await _identifier.IdentifyGamesAsync(new[] { "gamestash.ignore" });

            // Assert
            results.Should().BeEmpty();
        }
    }
}
