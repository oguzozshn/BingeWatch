using System.Security.Claims;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BingeWatch.API.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IFollowService _followService;
        private readonly IUserStatsService _statsService;

        public UsersController(UserManager<AppUser> userManager, IFollowService followService,
            IUserStatsService statsService)
        {
            _userManager = userManager;
            _followService = followService;
            _statsService = statsService;
        }

        /// <summary>Anonim istekte <c>null</c>; profil uç noktaları kimliği zorunlu kılmaz.</summary>
        private string? ViewerId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        [HttpGet("{username}")]
        public async Task<IActionResult> GetByUsername(string username)
        {
            var user = await _userManager.FindByNameAsync(username);
            // Gizli profil yalnızca sahibine görünür.
            if (user == null || (user.IsPrivate && user.Id != ViewerId))
                return NotFound();

            return Ok(new UserProfileDto
            {
                Username = user.UserName!,
                DisplayName = user.DisplayName,
                Bio = user.Bio,
                AvatarUrl = user.AvatarUrl,
                CreatedAt = user.CreatedAt,
                FollowerCount = await _followService.GetFollowerCountAsync(user.Id),
                FollowingCount = await _followService.GetFollowingCountAsync(user.Id),
                IsFollowedByViewer = ViewerId != null && await _followService.IsFollowingAsync(ViewerId, user.Id),
                IsViewer = user.Id == ViewerId
            });
        }

        /// <summary>Profil sayfasının istatistik bloğu: kartlar, favori diziler, yıllık özet.</summary>
        [HttpGet("{username}/stats")]
        public async Task<IActionResult> GetStats(string username)
        {
            var stats = await _statsService.GetStatsAsync(username, ViewerId);
            return stats == null ? NotFound() : Ok(stats);
        }

        [HttpGet("{username}/followers")]
        public async Task<IActionResult> GetFollowers(string username)
        {
            var followers = await _followService.GetFollowersAsync(username, ViewerId);
            return followers == null ? NotFound() : Ok(followers);
        }

        [HttpGet("{username}/following")]
        public async Task<IActionResult> GetFollowing(string username)
        {
            var following = await _followService.GetFollowingAsync(username, ViewerId);
            return following == null ? NotFound() : Ok(following);
        }

        [HttpPost("{username}/follow")]
        [Authorize]
        public async Task<IActionResult> Follow(string username) =>
            ToActionResult(await _followService.FollowAsync(ViewerId!, username));

        [HttpDelete("{username}/follow")]
        [Authorize]
        public async Task<IActionResult> Unfollow(string username) =>
            ToActionResult(await _followService.UnfollowAsync(ViewerId!, username));

        private IActionResult ToActionResult(FollowResult result) => result switch
        {
            FollowResult.TargetNotFound => NotFound(),
            FollowResult.Self => BadRequest(new { message = "Kendini takip edemezsin." }),
            _ => NoContent()
        };
    }
}
