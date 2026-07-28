using System.Security.Claims;
using BingeWatch.API.Configurations;
using BingeWatch.API.Dtos;
using BingeWatch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BingeWatch.API.Controllers
{
    /// <summary>
    /// Bölüm tartışmaları. Diziden bağımsız uç: yorumun hedefi bölüm, dizi id'si
    /// bilgiyi taşımıyor.
    /// </summary>
    [ApiController]
    [Route("api/episodes")]
    public class EpisodesController : ControllerBase
    {
        private readonly IEpisodeCommentService _commentService;

        public EpisodesController(IEpisodeCommentService commentService)
        {
            _commentService = commentService;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        private string? ViewerId => User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        /// <summary>
        /// Bölümün yorum ipliği. Anonim ve bölümü izlememiş kullanıcı **401 değil
        /// kilitli iplik** alır: arayüz "neden kapalı" mesajını gösterebilsin ve
        /// bölüm listesi anonim ziyaretçide de bozulmadan çizilsin.
        /// </summary>
        [HttpGet("{episodeId:int}/comments")]
        [AllowAnonymous]
        public async Task<IActionResult> GetComments(int episodeId)
        {
            var thread = await _commentService.GetThreadAsync(episodeId, ViewerId);
            return thread == null ? NotFound() : Ok(thread);
        }

        [HttpPost("{episodeId:int}/comments")]
        [Authorize]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> AddComment(int episodeId, [FromBody] AddEpisodeCommentRequest request)
        {
            var comment = await _commentService.AddAsync(CurrentUserId, episodeId, request);
            if (comment == null)
                return BadRequest(new { message = "Yorum boş olamaz ya da bölümü henüz izlemedin." });

            return Ok(comment);
        }

        [HttpDelete("comments/{commentId:int}")]
        [Authorize]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            var deleted = await _commentService.DeleteAsync(CurrentUserId, commentId);
            return deleted ? NoContent() : NotFound();
        }
    }
}
