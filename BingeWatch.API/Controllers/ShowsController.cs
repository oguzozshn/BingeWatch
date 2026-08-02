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
        private readonly ITmdbService _tmdbService;
        private readonly IRatingService _ratingService;

        public ShowsController(IShowCatalogService catalogService, IEpisodeProgressService progressService,
            ITmdbService tmdbService, IRatingService ratingService)
        {
            _catalogService = catalogService;
            _progressService = progressService;
            _tmdbService = tmdbService;
            _ratingService = ratingService;
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

        /// <summary>
        /// Bölüm sayfası. Rota sezon/bölüm numarasıyla kuruluyor, yerel id ile
        /// değil: id katalog yeniden tohumlanınca değişir, "S1E1" değişmez.
        /// Bölüm sayfası anonime de açık (dizi sayfasıyla aynı gerekçe, SEO);
        /// kişisel alanlar yalnızca kimlik doğrulanmışsa doluyor.
        /// </summary>
        [HttpGet("{tmdbId}/season/{seasonNumber}/episode/{episodeNumber}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetEpisode(int tmdbId, int seasonNumber, int episodeNumber)
        {
            var show = await _catalogService.GetOrSyncShowAsync(tmdbId);
            if (show == null)
                return NotFound(new { message = "Show not found on TMDb" });

            var season = show.Seasons.FirstOrDefault(s => s.SeasonNumber == seasonNumber);
            var episode = season?.Episodes.FirstOrDefault(e => e.EpisodeNumber == episodeNumber);
            if (season == null || episode == null)
                return NotFound(new { message = "Episode not found" });

            var userId = CurrentUserId;
            var watchedEpisodeIds = userId != null
                ? await _progressService.GetWatchedEpisodeIdsAsync(userId, tmdbId)
                : new HashSet<int>();

            decimal? myRating = null;
            var rewatchCount = 0;
            if (userId != null)
            {
                var ratings = await _ratingService.GetUserRatingsForShowAsync(userId, tmdbId);
                if (ratings != null && ratings.EpisodeRatings.TryGetValue(episode.Id, out var value))
                    myRating = value;

                rewatchCount = await _progressService.GetRewatchCountAsync(userId, episode.Id);
            }

            // Komşular sezon sınırını geçmeli: bir sezonun son bölümünden
            // sonraki, sonraki sezonun ilk bölümü.
            var flat = show.Seasons
                .OrderBy(s => s.SeasonNumber)
                .SelectMany(s => s.Episodes.OrderBy(e => e.EpisodeNumber)
                                           .Select(e => new EpisodeRefDto
                                           {
                                               SeasonNumber = s.SeasonNumber,
                                               EpisodeNumber = e.EpisodeNumber,
                                               Name = e.Name
                                           }))
                .ToList();

            var index = flat.FindIndex(r => r.SeasonNumber == seasonNumber && r.EpisodeNumber == episodeNumber);

            return Ok(new EpisodePageDto
            {
                TmdbShowId = show.TmdbId,
                ShowName = show.Name,
                SeasonNumber = season.SeasonNumber,
                SeasonName = season.Name,
                Id = episode.Id,
                EpisodeNumber = episode.EpisodeNumber,
                Name = episode.Name,
                Overview = episode.Overview,
                StillPath = episode.StillPath,
                AirDate = episode.AirDate,
                Runtime = episode.Runtime,
                TmdbVoteAverage = episode.TmdbVoteAverage,
                Watched = watchedEpisodeIds.Contains(episode.Id),
                MyRating = myRating,
                RewatchCount = rewatchCount,
                Previous = index > 0 ? flat[index - 1] : null,
                Next = index >= 0 && index < flat.Count - 1 ? flat[index + 1] : null
            });
        }

        /// <summary>Dizi sayfasındaki "Benzer" sekmesi.</summary>
        [HttpGet("{tmdbId}/similar")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSimilar(int tmdbId)
        {
            return Ok(await _tmdbService.GetSimilarSeriesAsync(tmdbId));
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

        /// <summary>
        /// Yeniden izleme ekler. İlerlemeyi ve dizi durumunu değiştirmez —
        /// yalnızca istatistikteki toplam süreye ve rewatch sayacına yansır.
        /// </summary>
        [HttpPost("{tmdbId}/episodes/{episodeId}/rewatch")]
        [Authorize]
        public async Task<IActionResult> AddRewatch(int tmdbId, int episodeId)
        {
            var count = await _progressService.AddRewatchAsync(CurrentUserId!, episodeId);
            if (count == null)
                return BadRequest(new { message = "Bölüm henüz izlenmemiş" });

            return Ok(new { rewatchCount = count });
        }

        [HttpDelete("{tmdbId}/episodes/{episodeId}/rewatch")]
        [Authorize]
        public async Task<IActionResult> RemoveRewatch(int tmdbId, int episodeId)
        {
            var count = await _progressService.RemoveLastRewatchAsync(CurrentUserId!, episodeId);
            if (count == null)
                return NotFound();

            return Ok(new { rewatchCount = count });
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
