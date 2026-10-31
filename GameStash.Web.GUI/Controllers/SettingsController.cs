using GameStash.Web.GUI.DTO;
using GameStash.Web.GUI.Interfaces;
using GameStash.Web.GUI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly ISettingsService _settingsService;

        public SettingsController(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GlobalSettingsDto))]
        public IActionResult GetSettings()
        {
            var dto = new GlobalSettingsDto
            {
                IgdbClientId = _settingsService.IgdbClientId,
                IgdbSecret = _settingsService.IgdbSecret,
                RootFolders = [.. _settingsService.RootFolders]
            };
            return Ok(dto);
        }

        [HttpPost("credentials")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> UpdateCredentials([FromBody] CredentialsUpdateDto request)
        {
            await _settingsService.UpdateIgdbCredentialsAsync(request.IgdbClientId, request.IgdbSecret);
            return NoContent();
        }
    }
}
