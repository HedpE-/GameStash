using GameStash.Core.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameStash.Core.Services
{
    public class GameIdentifier
    {
        // Método para identificar jogos e seus tipos a partir de caminhos de arquivo
        public IEnumerable<StashGame> IdentifyGames(IEnumerable<string> files)
        {
            ConcurrentBag<StashGame> identifiedGames = new ConcurrentBag<StashGame>();

            Parallel.ForEach(files, filePath =>
            {
                string directory = Path.GetDirectoryName(filePath);
                if (directory == null) return;

                string fileName = Path.GetFileName(filePath);
                string gameName = string.Empty;
                string repackType = string.Empty;

                // Verificar se o arquivo gamestash.json existe na pasta
                string gamestashFilePath = Path.Combine(directory, "gamestash.json");
                if (File.Exists(gamestashFilePath))
                {
                    // Ler o conteúdo do arquivo gamestash.json
                    string jsonContent = File.ReadAllText(gamestashFilePath);
                    try
                    {
                        var gamestashData = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);
                        if (gamestashData != null && gamestashData.ContainsKey("Name"))
                        {
                            gameName = gamestashData["Name"];
                            repackType = gamestashData.ContainsKey("Type") ? gamestashData["Type"] : "Other"; // Default para "Other" se Type não estiver presente
                        }
                    }
                    catch
                    {
                        // Se o JSON estiver malformado, prosseguir com o processo regular de identificação
                        gameName = string.Empty;
                        File.Delete(gamestashFilePath);
                    }
                }

                // Se o gamestash.json não foi encontrado ou não foi processado, identificar o jogo através de métodos regulares
                if (string.IsNullOrEmpty(gameName))
                {
                    var fileInfo = FileVersionInfo.GetVersionInfo(filePath);

                    if (IsFitGirlRepack(directory, fileInfo.LegalCopyright?.Trim() ?? string.Empty))
                    {
                        repackType = "FitGirl Repack";
                        gameName = fileInfo.ProductName?.Trim();
                        if (string.IsNullOrEmpty(gameName))
                            gameName = fileInfo.FileDescription?.Trim().Replace("Setup", "");
                    }
                    else if (IsDodiRepack(directory, fileInfo.CompanyName?.Trim() ?? string.Empty))
                    {
                        repackType = "Dodi Repack";
                        gameName = fileInfo.ProductName?.Trim();
                        if (string.IsNullOrEmpty(gameName))
                            gameName = fileInfo.FileDescription?.Trim().Replace("Setup", "");
                    }
                    else if (IsKaOsRepack(directory, fileInfo.CompanyName?.Trim() ?? string.Empty))
                    {
                        repackType = "KaOs Repack";
                        gameName = fileInfo.ProductName?.Trim();
                        if (string.IsNullOrEmpty(gameName))
                        {
                            gameName = fileInfo.FileDescription?.Trim().Replace("Setup", "");
                            if (string.IsNullOrEmpty(gameName))
                                gameName = ExtractGameNameFromKaOsNfo(directory);
                        }
                        if (string.IsNullOrEmpty(gameName))
                            gameName = gameName?.Replace(".REPACK-KaOs", "").Replace('.', ' ').Trim();
                    }
                    else if (IsTinyRepack(directory, fileInfo.CompanyName?.Trim() ?? string.Empty))
                    {
                        repackType = "Tiny Repack";
                        gameName = fileInfo.ProductName?.Trim();
                        if (string.IsNullOrEmpty(gameName))
                            gameName = fileInfo.FileDescription?.Trim().Replace("Setup", "");
                    }
                    else if (IsRgMechanicsRepack(directory, fileInfo.LegalCopyright?.Trim() ?? string.Empty))
                    {
                        repackType = "R.G. Mechanics";
                        gameName = fileInfo.ProductName?.Trim();
                        if (string.IsNullOrEmpty(gameName))
                            gameName = fileInfo.FileDescription?.Trim();
                    }
                    else if (IsMrDjRepack(directory))
                    {
                        repackType = "Mr DJ Repack";
                        gameName = new DirectoryInfo(directory).Name;
                    }
                    else if (IsElAmigosRepack(directory))
                    {
                        repackType = "El Amigos Repack";
                        gameName = File.ReadLines(Path.Combine(directory, "autorun.inf"))
                            .FirstOrDefault(line => line.StartsWith("label=", StringComparison.OrdinalIgnoreCase))?
                            .Split('=')[1];
                    }
                    else if (IsGogRelease(directory, fileInfo.CompanyName?.Trim() ?? string.Empty))
                    {
                        repackType = "GOG";
                        gameName = fileInfo.ProductName?.Trim();
                        if (string.IsNullOrEmpty(gameName))
                            gameName = fileInfo.FileDescription?.Trim().Replace("Setup", "");
                    }
                    else if (Path.GetExtension(fileName).ToLower() == ".exe" && (fileName.Contains("Install", StringComparison.OrdinalIgnoreCase) || fileName.Contains("Setup", StringComparison.OrdinalIgnoreCase)))
                    {
                        repackType = "Other";
                        gameName = Path.GetDirectoryName(directory);
                    }
                }

                // Se o nome do jogo foi identificado, adicioná-lo ao resultado
                if (!string.IsNullOrEmpty(gameName))
                {
                    identifiedGames.Add(new StashGame
                    {
                        PackageType = repackType,
                        MatchName = gameName,
                        Folder = directory
                    });

                    // Criar o arquivo gamestash.json caso ele não exista
                    if (!File.Exists(gamestashFilePath))
                    {
                        var gamestashData = new Dictionary<string, string>
                        {
                            { "Name", gameName },
                            { "Type", repackType }
                        };
                        var jsonString = JsonSerializer.Serialize(gamestashData);
                        File.WriteAllText(gamestashFilePath, jsonString);
                    }
                }
            });

            return identifiedGames.OrderBy(g => g.MatchName);
        }

        // Method to identify games and their types from file paths
        public async Task<IEnumerable<StashGame>> IdentifyGamesAsync(IEnumerable<string> files)
        {
            ConcurrentBag<StashGame> identifiedGames = await Task.Run(() => new ConcurrentBag<StashGame>());

            await Task.Run(() => Parallel.ForEach(files, filePath =>
            {
                if (filePath == "gamestash.ignore")
                    return;

                string directory = Path.GetDirectoryName(filePath);
                if (directory == null) return;

                string fileName = Path.GetFileName(filePath);
                string gameName = string.Empty;
                string repackType = string.Empty;

                // Verificar se o arquivo gamestash.json existe na pasta
                string gamestashFilePath = Path.Combine(directory, "gamestash.json");
                if (File.Exists(gamestashFilePath))
                {
                    // Ler o conteúdo do arquivo gamestash.json
                    string jsonContent = File.ReadAllText(gamestashFilePath);
                    try
                    {
                        var gamestashData = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);
                        if (gamestashData != null && gamestashData.ContainsKey("Name"))
                        {
                            gameName = gamestashData["Name"];
                            repackType = gamestashData.ContainsKey("Type") ? gamestashData["Type"] : "User Release"; // Default para "User Release" se Type não estiver presente
                        }
                    }
                    catch
                    {
                        // Se o JSON estiver malformado, prosseguir com o processo regular de identificação
                        gameName = string.Empty;
                        File.Delete(gamestashFilePath);
                    }
                }

                // Se o gamestash.json não foi encontrado ou não foi processado, identificar o jogo através de métodos regulares
                if (string.IsNullOrEmpty(gameName))
                {
                    var fileInfo = FileVersionInfo.GetVersionInfo(filePath);

                    if (IsFitGirlRepack(directory, fileInfo.LegalCopyright?.Trim() ?? string.Empty))
                    {
                        repackType = "FitGirl Repack";
                        gameName = fileInfo.ProductName?.Trim();
                        if (string.IsNullOrEmpty(gameName))
                            gameName = fileInfo.FileDescription?.Trim().Replace("Setup", "", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (IsDodiRepack(directory, fileInfo.CompanyName?.Trim() ?? string.Empty))
                    {
                        repackType = "Dodi Repack";
                        gameName = fileInfo.ProductName?.Trim();
                        if (string.IsNullOrEmpty(gameName))
                            gameName = fileInfo.FileDescription?.Trim().Replace("Setup", "", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (IsKaOsRepack(directory, fileInfo.CompanyName?.Trim() ?? string.Empty))
                    {
                        repackType = "KaOs Repack";
                        gameName = fileInfo.ProductName?.Trim();
                        if (string.IsNullOrEmpty(gameName))
                        {
                            gameName = fileInfo.FileDescription?.Trim().Replace("Setup", "", StringComparison.OrdinalIgnoreCase);
                            if (string.IsNullOrEmpty(gameName))
                                gameName = ExtractGameNameFromKaOsNfo(directory);
                        }
                        gameName = gameName?.Replace(".REPACK-KaOs", "", StringComparison.OrdinalIgnoreCase).Replace('.', ' ').Trim();
                    }
                    else if (IsTinyRepack(directory, fileInfo.CompanyName?.Trim() ?? string.Empty))
                    {
                        repackType = "Tiny Repack";
                        gameName = fileInfo.ProductName?.Trim();
                        if (string.IsNullOrEmpty(gameName))
                            gameName = fileInfo.FileDescription?.Trim().Replace("Setup", "", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (IsRgMechanicsRepack(directory, fileInfo.LegalCopyright?.Trim() ?? string.Empty))
                    {
                        repackType = "R.G. Mechanics";
                        gameName = fileInfo.ProductName?.Trim();
                        if (string.IsNullOrEmpty(gameName))
                            gameName = fileInfo.FileDescription?.Trim();
                    }
                    else if (IsMrDjRepack(directory))
                    {
                        repackType = "Mr DJ Repack";
                        gameName = new DirectoryInfo(directory).Name;
                    }
                    else if (IsElAmigosRepack(directory))
                    {
                        repackType = "El Amigos Repack";
                        gameName = File.ReadLines(Path.Combine(directory, "autorun.inf"))
                            .FirstOrDefault(line => line.StartsWith("label=", StringComparison.OrdinalIgnoreCase))?
                            .Split('=')[1];
                    }
                    else if (IsGogRelease(directory, fileInfo.CompanyName?.Trim() ?? string.Empty))
                    {
                        repackType = "GOG";
                        gameName = fileInfo.ProductName?.Trim();
                        if (string.IsNullOrEmpty(gameName))
                            gameName = fileInfo.FileDescription?.Trim().Replace("Setup", "", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (Path.GetExtension(fileName).ToLower() == ".exe" && (fileName.Contains("Install", StringComparison.OrdinalIgnoreCase) || fileName.Contains("Setup", StringComparison.OrdinalIgnoreCase)))
                    {
                        repackType = "Other";
                        gameName = Path.GetDirectoryName(directory);
                    }
                }

                // Se o nome do jogo foi identificado, adicioná-lo ao resultado
                if (!string.IsNullOrEmpty(gameName))
                {
                    identifiedGames.Add(new StashGame
                    {
                        PackageType = repackType,
                        MatchName = gameName,
                        Folder = directory
                    });

                    // Criar o arquivo gamestash.json caso ele não exista
                    if (!File.Exists(gamestashFilePath))
                    {
                        var gamestashData = new Dictionary<string, string>
                        {
                            { "Name", gameName },
                            { "Type", repackType }
                        };
                        var jsonString = JsonSerializer.Serialize(gamestashData);
                        File.WriteAllText(gamestashFilePath, jsonString);
                    }
                }
            }));

            return identifiedGames.OrderBy(g => g.MatchName);
        }

        // Method to identify if the folder contains FitGirl Repack files
        private static bool IsFitGirlRepack(string folderPath, string copyright)
        {
            if (copyright.Contains("FitGirl", StringComparison.OrdinalIgnoreCase))
                return true;

            bool hasFgFiles = Directory.GetFiles(folderPath, "fg-??.bin").Length > 0;
            bool hasFitgirlFiles = Directory.GetFiles(folderPath, "setup-fitgirl-?.bin").Length > 0;

            return hasFgFiles || hasFitgirlFiles;
        }

        // Method to identify if the folder contains Dodi Repack files
        private static bool IsDodiRepack(string folderPath, string companyName)
        {
            if (companyName.Contains("Dodi", StringComparison.OrdinalIgnoreCase))
                return true;

            bool hasDoiFiles = Directory.GetFiles(folderPath, "data?.doi").Length > 0;
            bool hasDdFiles = Directory.GetFiles(folderPath, "data?.dd").Length > 0;

            return hasDoiFiles || hasDdFiles;
        }

        // Method to identify if the folder contains KaOs Repack files
        private static bool IsKaOsRepack(string folderPath, string companyName)
        {
            if (companyName.Contains("KaOs", StringComparison.OrdinalIgnoreCase))
                return true;

            bool hasKaOsFiles = Directory.GetFiles(folderPath, "KaOs*.bin").Length > 0;

            return hasKaOsFiles;
        }

        // Método para extrair o nome do jogo do arquivo KaOs.nfo, se presente
        private static string ExtractGameNameFromKaOsNfo(string folderPath)
        {
            string nfoPath = Path.Combine(folderPath, "KaOs.nfo");
            if (File.Exists(nfoPath))
            {
                string content = File.ReadAllText(nfoPath);
                var match = Regex.Match(content, @"RELEASE\s*:\s*(?<name>.+?)\s*REPACK-KaOs", RegexOptions.IgnoreCase);
                if(match.Success)
                    return match.Groups["name"].Value;
            }

            // Se o arquivo NFO não existir, tentar extrair o nome a partir do nome da pasta
            string folderName = Path.GetFileName(folderPath);
            var folderMatch = Regex.Match(folderName, @"(?<name>.+?)REPACK-KaOs", RegexOptions.IgnoreCase);
            return folderMatch.Success ? folderMatch.Groups["name"].Value : string.Empty;
        }

        // Method to identify if the folder contains Tiny Repack files
        private static bool IsTinyRepack(string folderPath, string companyName)
        {
            if (companyName.Contains("MR Tiny", StringComparison.OrdinalIgnoreCase))
                return true;

            bool hasDataFiles = Directory.GetFiles(folderPath, "data??.tr").Length > 0;

            return hasDataFiles;
        }

        // Method to identify if the folder contains R.G. Mechanics files
        private static bool IsRgMechanicsRepack(string folderPath, string copyright)
        {
            if (copyright.Contains("R.G. Mechanics", StringComparison.OrdinalIgnoreCase))
                return true;

            bool hasDataFiles = Directory.GetFiles(folderPath, "data*.bin").Length > 0;

            return hasDataFiles;
        }

        // Method to identify if the folder contains MrDj Repack files
        private static bool IsMrDjRepack(string folderPath)
        {
            var files = Directory.GetFiles(folderPath);

            bool hasSetupFile = files.Any(f => Path.GetFileName(f).Equals("Setup.exe", StringComparison.OrdinalIgnoreCase));
            bool hasDataFiles = files.Any(f => {
                var name = Path.GetFileName(f);
                return name.StartsWith("Setup-", StringComparison.OrdinalIgnoreCase) &&
                       name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase);
            });
            bool hasAutorunFile = files.Any(f => Path.GetFileName(f).Equals("autorun.inf", StringComparison.OrdinalIgnoreCase));
            bool hasIconFile = files.Any(f => Path.GetFileName(f).Equals("Icon.ico", StringComparison.OrdinalIgnoreCase));

            return hasSetupFile && hasDataFiles && hasAutorunFile && hasIconFile;
        }

        // Method to identify if the folder contains El Amigos Repack files
        private static bool IsElAmigosRepack(string folderPath)
        {
            var files = Directory.GetFiles(folderPath);

            bool hasSetupFile = files.Any(f => Path.GetFileName(f).Equals("Setup.exe", StringComparison.OrdinalIgnoreCase));
            bool hasDataFiles = files.Any(f => {
                var name = Path.GetFileName(f);
                return name.StartsWith("elamigos-", StringComparison.OrdinalIgnoreCase) &&
                       name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase);
            });
            bool hasAutorunFile = files.Any(f => Path.GetFileName(f).Equals("autorun.inf", StringComparison.OrdinalIgnoreCase));
            bool hasIconFile = files.Any(f => Path.GetFileName(f).Equals("Icon.ico", StringComparison.OrdinalIgnoreCase));

            return hasSetupFile && hasDataFiles && hasAutorunFile && hasIconFile;
        }

        // Method to identify if the folder contains GOG files
        private static bool IsGogRelease(string folderPath, string companyName)
        {
            if (companyName.Contains("GOG.com", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
    }
}
