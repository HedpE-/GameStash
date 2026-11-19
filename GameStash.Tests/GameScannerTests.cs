using FluentAssertions;
using GameStash.Core.Services;
using GameStash.Tests.Config;
using NUnit.Framework;
using Serilog;

namespace GameStash.Core.Tests
{
    [TestFixture]
    public class GameScannerTests
    {
        private string _tempRootDir;
        private GameScanner _scanner;

        [SetUp]
        public void Setup()
        {
            TestLogInitializer.InitializeTestLogger();
            _tempRootDir = Path.Combine(Path.GetTempPath(), "GameStash_ScannerTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRootDir);
            _scanner = new GameScanner();
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_tempRootDir))
                Directory.Delete(_tempRootDir, recursive: true);

            Log.CloseAndFlush();
        }

        [Test]
        public void ScanFolder_WhenDirectoryDoesNotExist_ShouldThrowDirectoryNotFoundException()
        {
            var nonExistentPath = Path.Combine(_tempRootDir, "DoesNotExist");

            Action act = () => _scanner.ScanFolder(nonExistentPath);

            act.Should().Throw<DirectoryNotFoundException>();
        }

        [Test]
        public async Task ScanFolderAsync_WhenDirectoryDoesNotExist_ShouldThrowDirectoryNotFoundException()
        {
            var nonExistentPath = Path.Combine(_tempRootDir, "DoesNotExist");

            Func<Task> act = async () => await _scanner.ScanFolderAsync(nonExistentPath);

            await act.Should().ThrowAsync<DirectoryNotFoundException>();
        }

        [Test]
        public async Task ScanFolderAsync_ShouldDiscoverInstallersAndPrioritizeGamestashJson()
        {
            // Arrange
            // 1. Folder with gamestash.json AND setup.exe -> should only return gamestash.json
            var jsonGameDir = Path.Combine(_tempRootDir, "Cyberpunk 2077");
            Directory.CreateDirectory(jsonGameDir);
            var jsonPath = Path.Combine(jsonGameDir, "gamestash.json");
            await File.WriteAllTextAsync(jsonPath, "{\"Name\":\"Cyberpunk 2077\",\"Type\":\"GOG\"}");
            await File.WriteAllTextAsync(Path.Combine(jsonGameDir, "setup.exe"), "dummy");

            // 2. Folder with Install.exe only
            var exeGameDir = Path.Combine(_tempRootDir, "Hollow Knight");
            Directory.CreateDirectory(exeGameDir);
            var exePath = Path.Combine(exeGameDir, "Install.exe");
            await File.WriteAllTextAsync(exePath, "dummy");

            // 3. Folder with non-installer exe -> should be ignored
            var nonGameDir = Path.Combine(_tempRootDir, "RandomTools");
            Directory.CreateDirectory(nonGameDir);
            await File.WriteAllTextAsync(Path.Combine(nonGameDir, "unins000.exe"), "dummy");

            // Act
            var syncResults = _scanner.ScanFolder(_tempRootDir).ToList();
            var asyncResults = (await _scanner.ScanFolderAsync(_tempRootDir)).ToList();

            // Assert
            syncResults.Should().BeEquivalentTo(new[] { jsonPath, exePath });
            asyncResults.Should().BeEquivalentTo(syncResults);
        }

        [Test]
        public async Task ScanFolderAsync_WhenGamestashIgnoreExists_ShouldSkipFolderAndSubdirectories()
        {
            // Arrange
            var ignoredDir = Path.Combine(_tempRootDir, "IgnoredGame");
            var nestedSubDir = Path.Combine(ignoredDir, "SubFolder");
            Directory.CreateDirectory(nestedSubDir);

            await File.WriteAllTextAsync(Path.Combine(ignoredDir, "gamestash.ignore"), "");
            await File.WriteAllTextAsync(Path.Combine(ignoredDir, "setup.exe"), "dummy");
            await File.WriteAllTextAsync(Path.Combine(nestedSubDir, "install.exe"), "dummy");

            var validDir = Path.Combine(_tempRootDir, "ValidGame");
            Directory.CreateDirectory(validDir);
            var validExe = Path.Combine(validDir, "setup.exe");
            await File.WriteAllTextAsync(validExe, "dummy");

            // Act
            var syncResults = _scanner.ScanFolder(_tempRootDir).ToList();
            var asyncResults = (await _scanner.ScanFolderAsync(_tempRootDir)).ToList();

            // Assert
            syncResults.Should().ContainSingle().Which.Should().Be(validExe);
            asyncResults.Should().ContainSingle().Which.Should().Be(validExe);
        }
    }
}
