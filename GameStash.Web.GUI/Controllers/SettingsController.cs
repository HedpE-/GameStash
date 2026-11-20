using GameStash.Core.Models;
using GameStash.Core.Services;
using GameStash.Web.GUI.DTO;
using GameStash.Web.GUI.Interfaces;
using IgdbSharp;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController(ISettingsService settingsService) : ControllerBase
    {
        private readonly ISettingsService _settingsService = settingsService;

        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            var settings = await _settingsService.GetCurrentSettings();
            return Ok(settings);
        }

        [HttpGet("{apiName}/status")]
        public async Task<IActionResult> GetStatus([FromRoute] string apiName)
        {
            switch (apiName.ToLower())
            {
                case "igdb":
                    try
                    {
                        var igdbClient = await _settingsService.GetIgdbClient();
                        await igdbClient.GetAuthenticationStatusAsync();
                        return Ok(new { Success = true, Message = "IGDB connection operational." });
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.Contains("Twitch"))
                            return BadRequest(new { Message = "IGDB credentials failed Twitch authentication." });

                        return BadRequest(new { ex.Message });
                    }
                case "steam":
                    try
                    {
                        var steamClient = _settingsService.GetSteamClient();
                        await steamClient.GetAuthenticationStatusAsync();

                        return Ok(new { Success = true, Message = "Steam integration is operational." });
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(new { Message = $"Failed to communicate with Steam servers: {ex.Message}" });
                    }
                case "epic":
                    try
                    {
                        var epicClient = _settingsService.GetEpicClient();
                        var userData = await epicClient.VerifySessionAsync();

                        if (userData == null || string.IsNullOrWhiteSpace(userData.AccountId))
                            return BadRequest(new { Message = "Epic Games session has expired or is not configured." });

                        return Ok(new { Success = true, Username = userData.DisplayName, userData.DisplayName });
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(new { Message = $"Epic Games verification error: {ex.Message}" });
                    }

                case "gog":
                    try
                    {
                        var gogClient = _settingsService.GetGogClient();
                        var userData = await gogClient.GetUserDataAsync();

                        if (userData == null || !userData.IsLoggedIn)
                            return BadRequest(new { Message = "GOG session has expired or is invalid." });

                        return Ok(new { Success = true, Username = userData.Username });
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(new { Message = $"GOG verification error: {ex.Message}" });
                    }
                case "amazon":
                    try
                    {
                        var amazonClient = _settingsService.GetAmazonClient();
                        var userProfile = await amazonClient.GetUserProfileAsync();

                        if (userProfile == null || string.IsNullOrWhiteSpace(userProfile.UserId))
                            return BadRequest(new { Message = "Amazon Games session has expired or is invalid." });

                        return Ok(new { Success = true, Username = userProfile.Email, DisplayName = userProfile.Name });
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(new { Message = $"Amazon Games verification error: {ex.Message}" });
                    }
                default:
                    return BadRequest(new { Message = "Unsupported credentials type." });
            }
        }

        [HttpGet("epic/clientid")]
        public IActionResult GetEpicClientId()
        {
            return Ok(new
            {
                EpicClient.ClientId,
                LoginUrl = $"https://epicgames.com/id/api/redirect?clientId={EpicClient.ClientId}&responseType=code"
            });
        }

        [HttpGet("amazon/login-url")]
        public IActionResult GetAmazonLoginUrl()
        {
            var loginUrl = _settingsService.GenerateAmazonLoginUrl();
            return Ok(new { LoginUrl = loginUrl });
        }


        [HttpPut("{apiName}/credentials")]
        public async Task<IActionResult> UpdateCredentials([FromRoute] string apiName, [FromBody] CredentialsUpdateDto request)
        {
            try
            {
                await _settingsService.UpdateCredentialsAsync(apiName, request.UserClientId, request.Secret);
            }
            catch (Exception ex)
            {
                return BadRequest(new { ex.Message });
            }

            return Ok();
        }
    }
}