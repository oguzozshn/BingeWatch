using BingeWatch.API.Clients;
using BingeWatch.API.Configurations;
using BingeWatch.API.Dtos;
using BingeWatch.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BingeWatch.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WatchListController : ControllerBase
    {
        private readonly ITmdbService _tmdbService;
        private readonly IWatchListService _watchListService;

        public WatchListController(ITmdbService tmdbService, IWatchListService watchListService)
        {
            _tmdbService = tmdbService;
            _watchListService = watchListService;
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchSeries([FromQuery] string query, [FromQuery] int page = 1)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("Query parameter is required");

            var searchResults = await _tmdbService.SearchSeriesAsync(query, page);
            return Ok(searchResults);
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserWatchList(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest("UserId is required");

            var watchList = await _watchListService.GetUserWatchListAsync(userId);
            return Ok(watchList);
        }

        [HttpPost("user/{userId}/add")]
        public async Task<IActionResult> AddToWatchList(string userId, [FromBody] SeriesDto series)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest("UserId is required");

            if (series == null)
                return BadRequest("Series data is required");

            var success = await _watchListService.AddToWatchListAsync(userId, series);
            
            if (success)
                return Ok(new { message = "Series added to watchlist" });
            else
                return BadRequest(new { message = "Series already in watchlist or error occurred" });
        }

        [HttpDelete("user/{userId}/remove/{seriesId}")]
        public async Task<IActionResult> RemoveFromWatchList(string userId, int seriesId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest("UserId is required");

            var success = await _watchListService.RemoveFromWatchListAsync(userId, seriesId);
            
            if (success)
                return Ok(new { message = "Series removed from watchlist" });
            else
                return NotFound(new { message = "Series not found in watchlist" });
        }

        [HttpGet("user/{userId}/check/{seriesId}")]
        public async Task<IActionResult> CheckInWatchList(string userId, int seriesId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest("UserId is required");

            var isInWatchList = await _watchListService.IsInWatchListAsync(userId, seriesId);
            return Ok(new { isInWatchList });
        }

        [HttpPost("user/{userId}/toggle/{seriesId}")]
        public async Task<IActionResult> ToggleWatchlist(string userId, int seriesId)
        {
            var result = await _watchListService.ToggleAsync(userId, seriesId);

            return Ok(new { isInWatchList = result });
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus(string userId, int seriesId)
        {
            var result = await _watchListService.IsInWatchListAsync(userId, seriesId);
            return Ok(result);
        }

    }
}