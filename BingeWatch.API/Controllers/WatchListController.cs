using System.Security.Claims;
using BingeWatch.API.Clients;
using BingeWatch.API.Configurations;
using BingeWatch.API.Dtos;
using BingeWatch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BingeWatch.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WatchListController : ControllerBase
    {
        private readonly ITmdbService _tmdbService;
        private readonly IWatchListService _watchListService;
        private readonly IUserStatsService _statsService;

        public WatchListController(ITmdbService tmdbService, IWatchListService watchListService,
            IUserStatsService statsService)
        {
            _tmdbService = tmdbService;
            _watchListService = watchListService;
            _statsService = statsService;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<IActionResult> SearchSeries([FromQuery] string query, [FromQuery] int page = 1)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("Query parameter is required");

            var searchResults = await _tmdbService.SearchSeriesAsync(query, page);
            return Ok(searchResults);
        }

        [HttpGet("mine")]
        public async Task<IActionResult> GetMyWatchList()
        {
            var watchList = await _watchListService.GetUserWatchListAsync(CurrentUserId);
            return Ok(watchList);
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddToWatchList([FromBody] SeriesDto series)
        {
            if (series == null)
                return BadRequest("Series data is required");

            var success = await _watchListService.AddToWatchListAsync(CurrentUserId, series);

            if (success)
                return Ok(new { message = "Series added to watchlist" });
            else
                return BadRequest(new { message = "Series already in watchlist or error occurred" });
        }

        [HttpDelete("remove/{seriesId}")]
        public async Task<IActionResult> RemoveFromWatchList(int seriesId)
        {
            var success = await _watchListService.RemoveFromWatchListAsync(CurrentUserId, seriesId);

            if (success)
                return Ok(new { message = "Series removed from watchlist" });
            else
                return NotFound(new { message = "Series not found in watchlist" });
        }

        [HttpGet("check/{seriesId}")]
        public async Task<IActionResult> CheckInWatchList(int seriesId)
        {
            var isInWatchList = await _watchListService.IsInWatchListAsync(CurrentUserId, seriesId);
            return Ok(new { isInWatchList });
        }

        [HttpPost("toggle")]
        public async Task<IActionResult> ToggleWatchlist([FromBody] SeriesDto series)
        {
            var result = await _watchListService.ToggleAsync(CurrentUserId, series);
            return Ok(new { isInWatchList = result });
        }

        [HttpGet("{seriesId:int}/favorite")]
        public async Task<IActionResult> GetFavorite(int seriesId)
        {
            return Ok(await _statsService.IsFavoriteAsync(CurrentUserId, seriesId));
        }

        /// <summary>Diziyi favorilere ekler/çıkarır — profildeki "favori diziler" bloğunu besler.</summary>
        [HttpPut("{seriesId:int}/favorite")]
        public async Task<IActionResult> SetFavorite(int seriesId, [FromBody] SetFavoriteRequest request)
        {
            var success = await _statsService.SetFavoriteAsync(CurrentUserId, seriesId, request.IsFavorite);
            if (!success)
                return NotFound(new { message = "Dizi listende değil." });

            return Ok(new { isFavorite = request.IsFavorite });
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus(int seriesId)
        {
            var result = await _watchListService.IsInWatchListAsync(CurrentUserId, seriesId);
            return Ok(result);
        }

    }
}