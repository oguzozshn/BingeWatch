using System.Security.Claims;
using BingeWatch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BingeWatch.API.Controllers
{
    [ApiController]
    [Route("api/progress")]
    [Authorize]
    public class ProgressController : ControllerBase
    {
        private readonly IEpisodeProgressService _progressService;

        public ProgressController(IEpisodeProgressService progressService)
        {
            _progressService = progressService;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        /// <summary>Ana sayfa "Sırada ne var" paneli.</summary>
        [HttpGet("next-up")]
        public async Task<IActionResult> GetNextUp()
        {
            return Ok(await _progressService.GetNextUpAsync(CurrentUserId));
        }

        /// <summary>Yaklaşan bölümler takvimi.</summary>
        [HttpGet("upcoming")]
        public async Task<IActionResult> GetUpcoming([FromQuery] int days = 30)
        {
            return Ok(await _progressService.GetUpcomingEpisodesAsync(CurrentUserId, days));
        }
    }
}
