using System.Security.Claims;
using BingeWatch.API.Configurations;
using BingeWatch.API.Dtos;
using BingeWatch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BingeWatch.API.Controllers
{
    [ApiController]
    [Route("api/shows/{tmdbId:int}/ratings")]
    public class RatingsController : ControllerBase
    {
        private readonly IRatingService _ratingService;

        public RatingsController(IRatingService ratingService)
        {
            _ratingService = ratingService;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        /// <summary>Kullanıcının bu dizideki tüm puanları (dizi + sezon + bölüm).</summary>
        [HttpGet("mine")]
        [Authorize]
        public async Task<IActionResult> GetMine(int tmdbId)
        {
            var ratings = await _ratingService.GetUserRatingsForShowAsync(CurrentUserId, tmdbId);
            if (ratings == null)
                return NotFound();

            return Ok(ratings);
        }

        /// <summary>Dizinin BingeWatch kullanıcı ortalaması + dağılım histogramı.</summary>
        [HttpGet("summary")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSummary(int tmdbId)
        {
            var summary = await _ratingService.GetShowSummaryAsync(tmdbId);
            if (summary == null)
                return NotFound();

            return Ok(summary);
        }

        /// <summary>Takip edilenlerin bu diziye verdiği puanlar (dizi seviyesi).</summary>
        [HttpGet("friends")]
        [Authorize]
        public async Task<IActionResult> GetFriendRatings(int tmdbId)
        {
            var friends = await _ratingService.GetFriendRatingsAsync(CurrentUserId, tmdbId);
            if (friends == null)
                return NotFound();

            return Ok(friends);
        }

        [HttpPut]
        [Authorize]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> SetRating(int tmdbId, [FromBody] SetRatingRequest request)
        {
            var rating = await _ratingService.SetRatingAsync(CurrentUserId, tmdbId, request);
            if (rating == null)
                return BadRequest(new { message = "Geçersiz puan ya da bulunamayan hedef." });

            return Ok(rating);
        }

        [HttpDelete]
        [Authorize]
        public async Task<IActionResult> RemoveRating(int tmdbId, [FromBody] SetRatingRequest request)
        {
            var removed = await _ratingService.RemoveRatingAsync(CurrentUserId, tmdbId, request);
            if (!removed)
                return NotFound();

            return NoContent();
        }
    }
}
