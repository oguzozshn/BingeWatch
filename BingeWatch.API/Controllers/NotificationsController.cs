using System.Security.Claims;
using BingeWatch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BingeWatch.API.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? cursor = null, [FromQuery] int take = 30)
        {
            return Ok(await _notificationService.GetAsync(CurrentUserId, cursor, take));
        }

        /// <summary>Navbar'daki zil rozeti bunu okur.</summary>
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            return Ok(await _notificationService.GetUnreadCountAsync(CurrentUserId));
        }

        [HttpPost("read")]
        public async Task<IActionResult> MarkAllRead()
        {
            return Ok(new { marked = await _notificationService.MarkAllReadAsync(CurrentUserId) });
        }
    }
}
