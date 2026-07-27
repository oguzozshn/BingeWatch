using System.Security.Claims;
using BingeWatch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BingeWatch.API.Controllers
{
    [ApiController]
    [Route("api/feed")]
    [Authorize]
    public class FeedController : ControllerBase
    {
        private readonly IActivityService _activityService;

        public FeedController(IActivityService activityService)
        {
            _activityService = activityService;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        /// <summary>Takip edilenlerin (ve kullanıcının kendi) aktiviteleri, en yeniden eskiye.</summary>
        [HttpGet]
        public async Task<IActionResult> GetFeed([FromQuery] int skip = 0, [FromQuery] int take = 20)
        {
            return Ok(await _activityService.GetFeedAsync(CurrentUserId, skip, take));
        }
    }
}
