using System.Security.Claims;
using BingeWatch.API.Configurations;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BingeWatch.API.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IFollowService _followService;
        private readonly IUserStatsService _statsService;
        private readonly IUserListService _listService;
        private readonly IUserLibraryService _libraryService;
        private readonly IBlockService _blockService;

        public UsersController(UserManager<AppUser> userManager, IFollowService followService,
            IUserStatsService statsService, IUserListService listService,
            IUserLibraryService libraryService, IBlockService blockService)
        {
            _userManager = userManager;
            _followService = followService;
            _statsService = statsService;
            _listService = listService;
            _libraryService = libraryService;
            _blockService = blockService;
        }

        /// <summary>Anonim istekte <c>null</c>; profil uç noktaları kimliği zorunlu kılmaz.</summary>
        private string? ViewerId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        /// <summary>
        /// Kullanıcı arama. Rota <c>{username}</c>'den önce tanımlı olmak
        /// zorunda değil — ASP.NET Core literal segmenti parametreliye tercih
        /// ediyor — ama okuyanın kafası karışmasın diye yine de yukarıda.
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(q))
                return Ok(new List<UserSummaryDto>());

            return Ok(await _followService.SearchAsync(q, ViewerId, Math.Clamp(limit, 1, 50)));
        }

        [HttpGet("{username}")]
        public async Task<IActionResult> GetByUsername(string username)
        {
            var user = await _userManager.FindByNameAsync(username);
            // Gizli profil yalnızca sahibine görünür.
            if (user == null || (user.IsPrivate && user.Id != ViewerId))
                return NotFound();

            // Engelli taraflar birbirinin profilini göremez — hangi yönde engellendiği
            // de sızmasın diye ikisine de 404. Engeli kaldırma /api/users/me/blocks
            // üzerinden yapılır, karşı tarafın profilinden değil.
            if (await _blockService.IsBlockedBetweenAsync(ViewerId, user.Id))
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
                IsViewer = user.Id == ViewerId,
                IsPrivate = user.Id == ViewerId ? user.IsPrivate : null
            });
        }

        /// <summary>
        /// Kendi profilini düzenleme. <c>IsPrivate</c> buraya kadar ölü koddu:
        /// gizlilik kuralı takip, istatistik, liste ve arama servislerinin
        /// hepsinde uygulanıyordu ama kullanıcının bayrağı açacak bir yeri yoktu.
        /// </summary>
        [HttpPut("me")]
        [Authorize]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var user = await _userManager.FindByIdAsync(ViewerId!);
            if (user == null)
                return NotFound();

            if (!ProfileValidator.TryNormalize(request, out var clean, out var error))
                return BadRequest(new { message = error });

            user.DisplayName = clean.DisplayName!;
            user.Bio = clean.Bio;
            user.AvatarUrl = clean.AvatarUrl;
            user.IsPrivate = clean.IsPrivate;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });

            return NoContent();
        }

        /// <summary>Profil sayfasının istatistik bloğu: kartlar, favori diziler, yıllık özet.</summary>
        [HttpGet("{username}/stats")]
        public async Task<IActionResult> GetStats(string username)
        {
            var stats = await _statsService.GetStatsAsync(username, ViewerId);
            return stats == null ? NotFound() : Ok(stats);
        }

        /// <summary>İstatistik sayfasının tamamı; profil bloğundan ayrı ve daha ağır.</summary>
        [HttpGet("{username}/stats/detail")]
        public async Task<IActionResult> GetDetailedStats(string username)
        {
            var stats = await _statsService.GetDetailedStatsAsync(username, ViewerId);
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

        /// <summary>
        /// Kullanıcının kütüphanesi — listesindeki diziler, durumlarıyla. Arayüz
        /// bunu "izledikleri" ve "izleyecekleri" diye ikiye bölüyor; sekme
        /// değiştirmek yeni istek gerektirmesin diye uç tek yanıt veriyor.
        /// </summary>
        [HttpGet("{username}/library")]
        public async Task<IActionResult> GetLibrary(string username)
        {
            var library = await _libraryService.GetLibraryAsync(username, ViewerId);
            return library == null ? NotFound() : Ok(library);
        }

        /// <summary>Kullanıcının listeleri; kapalı olanlar yalnızca sahibine döner.</summary>
        [HttpGet("{username}/lists")]
        public async Task<IActionResult> GetLists(string username)
        {
            var lists = await _listService.GetForUserAsync(username, ViewerId);
            return lists == null ? NotFound() : Ok(lists);
        }

        [HttpPost("{username}/follow")]
        [Authorize]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> Follow(string username) =>
            ToActionResult(await _followService.FollowAsync(ViewerId!, username));

        [HttpDelete("{username}/follow")]
        [Authorize]
        public async Task<IActionResult> Unfollow(string username) =>
            ToActionResult(await _followService.UnfollowAsync(ViewerId!, username));

        /// <summary>İsteği yapanın engellediği kullanıcılar — ayarlardaki engel listesi.</summary>
        [HttpGet("me/blocks")]
        [Authorize]
        public async Task<IActionResult> GetBlocked() =>
            Ok(await _blockService.GetBlockedAsync(ViewerId!));

        [HttpPost("{username}/block")]
        [Authorize]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> Block(string username) =>
            ToActionResult(await _blockService.BlockAsync(ViewerId!, username));

        [HttpDelete("{username}/block")]
        [Authorize]
        public async Task<IActionResult> Unblock(string username) =>
            ToActionResult(await _blockService.UnblockAsync(ViewerId!, username));

        private IActionResult ToActionResult(FollowResult result) => result switch
        {
            FollowResult.TargetNotFound => NotFound(),
            FollowResult.Self => BadRequest(new { message = "Kendini takip edemezsin." }),
            _ => NoContent()
        };

        private IActionResult ToActionResult(BlockResult result) => result switch
        {
            BlockResult.TargetNotFound => NotFound(),
            BlockResult.Self => BadRequest(new { message = "Kendini engelleyemezsin." }),
            _ => NoContent()
        };
    }
}
