using GameStash.Web.GUI.DTO;
using GameStash.Web.GUI.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration.UserSecrets;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController(ISettingsService settingsService) : ControllerBase
    {
        private readonly ISettingsService _settingsService = settingsService;

        [HttpGet]
        public IActionResult GetSettings()
        {
            var dto = new GlobalSettingsDto
            {
                IgdbClientId = _settingsService.IgdbClientId,
                IgdbSecret = _settingsService.IgdbSecret
            };
            return Ok(dto);
        }

        [HttpPut("credentials")]
        public async Task<IActionResult> UpdateCredentials([FromBody] CredentialsUpdateDto request)
        {
            await _settingsService.UpdateIgdbCredentialsAsync(request.IgdbClientId, request.IgdbSecret);
            return NoContent();
        }
    }
}