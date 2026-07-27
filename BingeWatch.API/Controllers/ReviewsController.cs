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
    [Route("api/reviews")]
    public class ReviewsController : ControllerBase
    {
        private readonly IReviewService _reviewService;
        private readonly IReviewInteractionService _interactionService;

        public ReviewsController(IReviewService reviewService, IReviewInteractionService interactionService)
        {
            _reviewService = reviewService;
            _interactionService = interactionService;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        /// <summary>Anonim istekte <c>null</c> — beğeni durumu boş döner.</summary>
        private string? ViewerId => User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        /// <summary>Genel inceleme akışı — <c>/reviews</c> sayfası bunu okur.</summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetFeed([FromQuery] int skip = 0, [FromQuery] int take = 20,
            [FromQuery] ReviewSort sort = ReviewSort.Newest)
        {
            return Ok(await _reviewService.GetFeedAsync(skip, take, sort, ViewerId));
        }

        [HttpGet("show/{tmdbId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetForShow(int tmdbId, [FromQuery] int? seasonNumber = null)
        {
            return Ok(await _reviewService.GetForShowAsync(tmdbId, seasonNumber, ViewerId));
        }

        [HttpGet("show/{tmdbId:int}/mine")]
        [Authorize]
        public async Task<IActionResult> GetOwnForShow(int tmdbId)
        {
            return Ok(await _reviewService.GetOwnForShowAsync(CurrentUserId, tmdbId));
        }

        [HttpPut("show/{tmdbId:int}")]
        [Authorize]
        [EnableRateLimiting(RateLimitPolicies.Write)]
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

        [HttpPost("{reviewId:int}/like")]
        [Authorize]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> Like(int reviewId)
        {
            var state = await _interactionService.LikeAsync(CurrentUserId, reviewId);
            return state == null ? NotFound() : Ok(state);
        }

        [HttpDelete("{reviewId:int}/like")]
        [Authorize]
        public async Task<IActionResult> Unlike(int reviewId)
        {
            var state = await _interactionService.UnlikeAsync(CurrentUserId, reviewId);
            return state == null ? NotFound() : Ok(state);
        }

        [HttpGet("{reviewId:int}/comments")]
        [AllowAnonymous]
        public async Task<IActionResult> GetComments(int reviewId)
        {
            var comments = await _interactionService.GetCommentsAsync(reviewId, ViewerId);
            return comments == null ? NotFound() : Ok(comments);
        }

        [HttpPost("{reviewId:int}/comments")]
        [Authorize]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> AddComment(int reviewId, [FromBody] AddCommentRequest request)
        {
            var comment = await _interactionService.AddCommentAsync(CurrentUserId, reviewId, request);
            if (comment == null)
                return BadRequest(new { message = "Yorum boş olamaz ya da inceleme bulunamadı." });

            return Ok(comment);
        }

        [HttpDelete("comments/{commentId:int}")]
        [Authorize]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            var deleted = await _interactionService.DeleteCommentAsync(CurrentUserId, commentId);
            return deleted ? NoContent() : NotFound();
        }
    }
}
