using GameStash.Web.GUI.DTO;
using GameStash.Web.GUI.Interfaces;
using GameStash.Web.GUI.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Controllers
{
    [ApiController]
    [Route("api/home")]
    public class HomeController(IHomeDashboardService dashboardService) : ControllerBase
    {
        private readonly IHomeDashboardService _dashboardService = dashboardService;

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var data = await _dashboardService.GetDashboardDataAsync();
            return Ok(data);
        }
    }
}
