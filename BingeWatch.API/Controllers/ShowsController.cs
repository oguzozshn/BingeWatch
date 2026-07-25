using System.Security.Claims;
using BingeWatch.API.Dtos;
using BingeWatch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BingeWatch.API.Controllers
{
    [ApiController]
    [Route("api/shows")]
    public class ShowsController : ControllerBase
    {
        private readonly IShowCatalogService _catalogService;
        private readonly IEpisodeProgressService _progressService;

        public ShowsController(IShowCatalogService catalogService, IEpisodeProgressService progressService)
        {
            _catalogService = catalogService;
            _progressService = progressService;
        }

        private string? CurrentUserId => User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        /// <summary>
        /// Dizi + sezon + bölüm detayları. Yerel katalogda yoksa veya bayatsa TMDb'den
        /// senkronize edilerek döner (bkz. ShowCatalogService).
        /// </summary>
        [HttpGet("{tmdbId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetShow(int tmdbId)
        {
            var show = await _catalogService.GetOrSyncShowAsync(tmdbId);
            if (show == null)
                return NotFound(new { message = "Show not found on TMDb" });

            var userId = CurrentUserId;
            var watchedEpisodeIds = userId != null
                ? await _progressService.GetWatchedEpisodeIdsAsync(userId, tmdbId)
                : new HashSet<int>();

            var dto = new ShowDetailDto
            {
                TmdbId = show.TmdbId,
                ImdbId = show.ImdbId,
                Name = show.Name,
                Overview = show.Overview,
                PosterPath = show.PosterPath,
                BackdropPath = show.BackdropPath,
                FirstAirDate = show.FirstAirDate,
                Status = show.TmdbStatus,
                VoteAverage = show.VoteAverage,
                VoteCount = show.VoteCount,
                Seasons = show.Seasons
                    .OrderBy(s => s.SeasonNumber)
                    .Select(s => new SeasonDetailDto
                    {
                        SeasonNumber = s.SeasonNumber,
                        Name = s.Name,
                        AirDate = s.AirDate,
                        Episodes = s.Episodes
                            .OrderBy(e => e.EpisodeNumber)
                            .Select(e => new EpisodeDetailDto
                            {
                                Id = e.Id,
                                EpisodeNumber = e.EpisodeNumber,
                                Name = e.Name,
                                StillPath = e.StillPath,
                                AirDate = e.AirDate,
                                Runtime = e.Runtime,
                                TmdbVoteAverage = e.TmdbVoteAverage,
                                Watched = watchedEpisodeIds.Contains(e.Id)
                            }).ToList()
                    }).ToList()
            };

            return Ok(dto);
        }

        [HttpGet("{tmdbId}/progress")]
        [Authorize]
        public async Task<IActionResult> GetProgress(int tmdbId)
        {
            var progress = await _progressService.GetShowProgressAsync(CurrentUserId!, tmdbId);
            if (progress == null)
                return NotFound();

            return Ok(progress);
        }

        [HttpPost("{tmdbId}/episodes/{episodeId}/watched")]
        [Authorize]
        public async Task<IActionResult> SetEpisodeWatched(int tmdbId, int episodeId, [FromBody] MarkWatchedRequest request)
        {
            var success = await _progressService.SetEpisodeWatchedAsync(CurrentUserId!, episodeId, request.Watched);
            if (!success)
                return NotFound();

            return Ok(await _progressService.GetShowProgressAsync(CurrentUserId!, tmdbId));
        }

        [HttpPost("{tmdbId}/seasons/{seasonNumber}/watched")]
        [Authorize]
        public async Task<IActionResult> SetSeasonWatched(int tmdbId, int seasonNumber, [FromBody] MarkWatchedRequest request)
        {
            var affected = await _progressService.SetSeasonWatchedAsync(CurrentUserId!, tmdbId, seasonNumber, request.Watched);
            return Ok(new { affectedEpisodes = affected, progress = await _progressService.GetShowProgressAsync(CurrentUserId!, tmdbId) });
        }

        [HttpPost("{tmdbId}/watched-up-to/{episodeId}")]
        [Authorize]
        public async Task<IActionResult> SetWatchedUpTo(int tmdbId, int episodeId)
        {
            var affected = await _progressService.SetWatchedUpToAsync(CurrentUserId!, tmdbId, episodeId);
            return Ok(new { affectedEpisodes = affected, progress = await _progressService.GetShowProgressAsync(CurrentUserId!, tmdbId) });
        }
    }
}
