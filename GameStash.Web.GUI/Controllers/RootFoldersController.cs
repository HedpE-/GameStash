using GameStash.Web.GUI.DTO;
using GameStash.Web.GUI.Interfaces;
using GameStash.Web.GUI.Services;
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

        [HttpGet("found/match")]
        public async Task<IActionResult> MatchGame([FromQuery] string path, [FromQuery] string searchText = null)
        {
            var game = _rootFoldersService.FoundFolders
                .FirstOrDefault(f => string.Equals(f.Folder, path, StringComparison.OrdinalIgnoreCase));

            if (game == null) return NotFound("Target folder not registered.");

            if (string.IsNullOrWhiteSpace(searchText))
                searchText = game.MatchName ?? game.SortName ?? game.Name ?? string.Empty;
            game.MatchName = searchText;

            var igdbClient = _settingsService.GetIgdbClient();
            await game.MatchIgdbGame(igdbClient, requestPriority: RequestPriority.High);

            if (game.IgdbMatches != null && game.IgdbMatches.Any())
            {
                game.IgdbSelectedMatch = game.IgdbMatches.First();
            }

            return Ok(FoundFolderDto.FromModel(game));
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