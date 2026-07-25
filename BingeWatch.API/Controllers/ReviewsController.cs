using System.Security.Claims;
using BingeWatch.API.Dtos;
using BingeWatch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BingeWatch.API.Controllers
{
    [ApiController]
    [Route("api/reviews")]
    public class ReviewsController : ControllerBase
    {
        private readonly IReviewService _reviewService;

        public ReviewsController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        /// <summary>Genel inceleme akışı — <c>/reviews</c> sayfası bunu okur.</summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetFeed([FromQuery] int skip = 0, [FromQuery] int take = 20,
            [FromQuery] ReviewSort sort = ReviewSort.Newest)
        {
            return Ok(await _reviewService.GetFeedAsync(skip, take, sort));
        }

        [HttpGet("show/{tmdbId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetForShow(int tmdbId, [FromQuery] int? seasonNumber = null)
        {
            return Ok(await _reviewService.GetForShowAsync(tmdbId, seasonNumber));
        }

        [HttpGet("show/{tmdbId:int}/mine")]
        [Authorize]
        public async Task<IActionResult> GetOwnForShow(int tmdbId)
        {
            return Ok(await _reviewService.GetOwnForShowAsync(CurrentUserId, tmdbId));
        }

        [HttpPut("show/{tmdbId:int}")]
        [Authorize]
        public async Task<IActionResult> Upsert(int tmdbId, [FromBody] UpsertReviewRequest request)
        {
            var review = await _reviewService.UpsertAsync(CurrentUserId, tmdbId, request);
            if (review == null)
                return BadRequest(new { message = "İnceleme boş olamaz ya da hedef bulunamadı." });

            return Ok(review);
        }

        [HttpDelete("{reviewId:int}")]
        [Authorize]
        public async Task<IActionResult> Delete(int reviewId)
        {
            var deleted = await _reviewService.DeleteAsync(CurrentUserId, reviewId);
            if (!deleted)
                return NotFound();

            return NoContent();
        }
    }
}
