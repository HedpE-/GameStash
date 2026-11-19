using GameStash.Web.GUI.DTO;
using GameStash.Web.GUI.Interfaces;
using IgdbSharp.Enumerators;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Controllers
{
    [ApiController]
    [Route("api/folders")]
    public class RootFoldersController(IRootFoldersService rootFoldersService, ISettingsService settingsService) : ControllerBase
    {
        private readonly IRootFoldersService _rootFoldersService = rootFoldersService;
        private readonly ISettingsService _settingsService = settingsService;

        [HttpGet]
        public IActionResult GetRootFolders()
        {
            try
            {
                return Ok(_rootFoldersService.RootFolders.Select(f => RootFolderDto.FromModel(f)));
            }
            catch
            {
                return BadRequest("An error has occured.");
            }
        }

        [HttpPost]
        [HttpPut]
        public async Task<IActionResult> AddFolder([FromBody] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return BadRequest("O caminho da pasta não pode ser vazio.");

            try
            {
                await _rootFoldersService.AddRootFolderAsync(path);
                return Ok();
            }
            catch (DirectoryNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> RemoveFolder([FromRoute] int id)
        {
            var rootFolder = _rootFoldersService.RootFolders.FirstOrDefault(f => f.Id == id);
            if (rootFolder == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(rootFolder.Path))
                return BadRequest("O caminho da pasta é obrigatório.");

            await _rootFoldersService.RemoveRootFolderAsync(rootFolder.Path);
            return Ok();
        }

        [HttpGet("browse")]
        public IActionResult BrowseFolder()
        {
            try
            {
                string selectedPath = string.Empty;

                var thread = new System.Threading.Thread(() =>
                {
                    try
                    {
                        var dlg = new Microsoft.Win32.OpenFolderDialog
                        {
                            Title = "Select game installation root folder"
                        };

                        if (dlg.ShowDialog() == true)
                        {
                            selectedPath = dlg.FolderName;
                        }
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "Failed to instantiate or show WPF OpenFolderDialog on STA thread.");
                    }
                });

                thread.SetApartmentState(System.Threading.ApartmentState.STA);
                thread.Start();
                thread.Join();

                return Ok(new { Path = selectedPath });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Folder browsing is not supported in this execution context.");
                return StatusCode(500, new { Message = "Native folder picker is unavailable in this environment. Please type or paste the path manually." });
            }
        }

        [HttpGet("found")]
        public IActionResult GetFoundFolders()
        {
            try
            {
                return Ok(_rootFoldersService.FoundFolders.Select(f => FoundFolderDto.FromModel(f)));
            }
            catch
            {
                return BadRequest("An error has occured.");
            }
        }

        [HttpDelete("found")]
        public IActionResult IgnoreFoundFolder([FromQuery] string folderPath)
        {
            try
            {
                _rootFoldersService.IgnoreFoundFolder(folderPath);

                return Ok();
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("found/match")]
        public async Task<IActionResult> MatchGame([FromQuery] string path, [FromQuery] string searchText = null)
        {
            var game = _rootFoldersService.FoundFolders
                .FirstOrDefault(f => string.Equals(f.Folder, path, StringComparison.OrdinalIgnoreCase));

            if (game == null) return NotFound("Target folder not registered.");

            if (string.IsNullOrWhiteSpace(searchText))
                searchText = game.MatchName ?? game.SortName ?? game.Name ?? string.Empty;
            game.MatchName = searchText;

            var igdbClient = await _settingsService.GetIgdbClient();
            await game.MatchIgdbGame(igdbClient, requestPriority: RequestPriority.High);

            if (game.IgdbMatches != null && game.IgdbMatches.Any())
            {
                game.IgdbSelectedMatch = game.IgdbMatches.First();
                if (game.MatchName.Contains(game.IgdbSelectedMatch.Name, StringComparison.InvariantCultureIgnoreCase))
                    game.IsIgdbMatched = true;
            }

            return Ok(FoundFolderDto.FromModel(game));
        }

        [HttpPut("found/match/{id:int}")]
        public async Task<IActionResult> MatchGame([FromRoute] int id, [FromQuery] string folderPath)
        {
            var game = _rootFoldersService.FoundFolders
                .FirstOrDefault(f => string.Equals(f.Folder, folderPath, StringComparison.OrdinalIgnoreCase));

            if (game == null) return NotFound("Target folder not registered.");

            var match = game.IgdbMatches.FirstOrDefault(m => m.Id == id);
            if (match != null)
            {
                game.IgdbSelectedMatch = match;
                game.IsIgdbMatched = true;

                return Ok();
            }

            return NotFound("Match not found.");
        }

        [HttpPut("rescan")]
        public async Task<IActionResult> RescanFolder()
        {
            var task = _rootFoldersService.RefreshAllRootFoldersAsync();

            if (task.Status != TaskStatus.Canceled && task.Status != TaskStatus.Faulted)
                return Accepted();

            return StatusCode(422);
        }

        [HttpPost("rescan/{id:int}")]
        public async Task<IActionResult> RescanFolder([FromRoute] int id)
        {
            var rootFolder = _rootFoldersService.RootFolders.FirstOrDefault(f => f.Id == id);
            if (rootFolder == null)
                return NotFound();

            var task = _rootFoldersService.RefreshRootFolderAsync(rootFolder.Path);

            if (task.Status != TaskStatus.Canceled && task.Status != TaskStatus.Faulted)
                return Accepted();
            else
                return StatusCode(422);

        }
    }
}