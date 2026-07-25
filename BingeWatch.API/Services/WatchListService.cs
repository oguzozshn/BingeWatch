using Microsoft.EntityFrameworkCore;
using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using Microsoft.Extensions.Logging;

namespace BingeWatch.API.Services
{
    public class WatchListService : IWatchListService
    {
        private readonly BingeOnDbContext _context;
        private readonly ILogger<WatchListService> _logger;

        public WatchListService(BingeOnDbContext context, ILogger<WatchListService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<SeriesDto>> GetUserWatchListAsync(string userId)
        {
            var watchListItems = await _context.WatchListItems
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.AddedDate)
                .ToListAsync();

            return watchListItems.Select(item => new SeriesDto
            {
                Id = item.SeriesId,
                Name = item.SeriesName,
                Overview = item.Overview,
                PosterPath = item.PosterPath,
                FirstAirDate = item.FirstAirDate
            }).ToList();
        }

        public async Task<bool> AddToWatchListAsync(string userId, SeriesDto series)
        {
            _logger.LogInformation("Adding to watchlist: userId={UserId}, seriesId={SeriesId}, name={Name}", userId, series?.Id, series?.Name);

            try
            {
                var existingItem = await _context.WatchListItems
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.SeriesId == series.Id);

                if (existingItem != null)
                {
                    _logger.LogInformation("Series {SeriesId} already in watchlist for user {UserId}", series.Id, userId);
                    return false;
                }

                var watchListItem = new WatchListItem
                {
                    SeriesId = series.Id,
                    SeriesName = series.Name ?? "",
                    Overview = series.Overview ?? "",
                    PosterPath = NormalizePosterPath(series.PosterPath),
                    FirstAirDate = series.FirstAirDate,
                    UserId = userId,
                    AddedDate = DateTime.UtcNow
                };

                _context.WatchListItems.Add(watchListItem);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Series {SeriesId} added to watchlist for user {UserId}", series.Id, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding series {SeriesId} to watchlist for user {UserId}", series?.Id, userId);
                return false;
            }
        }

        public async Task<bool> RemoveFromWatchListAsync(string userId, int seriesId)
        {
            try
            {
                var item = await _context.WatchListItems
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.SeriesId == seriesId);

                if (item == null)
                {
                    return false;
                }

                _context.WatchListItems.Remove(item);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing series {SeriesId} from watchlist for user {UserId}", seriesId, userId);
                return false;
            }
        }

        public async Task<bool> IsInWatchListAsync(string userId, int seriesId)
        {
            return await _context.WatchListItems
                .AnyAsync(w => w.UserId == userId && w.SeriesId == seriesId);
        }

        public async Task<bool> ToggleAsync(string userId, SeriesDto series)
        {
            _logger.LogInformation("Toggling watchlist: userId={UserId}, seriesId={SeriesId}, name={Name}", userId, series?.Id, series?.Name);

            var existing = await _context.WatchListItems
                .FirstOrDefaultAsync(x => x.UserId == userId && x.SeriesId == series.Id);

            if (existing == null)
            {
                var added = await AddToWatchListAsync(userId, series);
                return added;
            }

            try
            {
                _context.WatchListItems.Remove(existing);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Series {SeriesId} removed from watchlist for user {UserId}", series.Id, userId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing series {SeriesId} from watchlist for user {UserId} during toggle", series.Id, userId);
                // Removal failed - the item is still in the watchlist, so report the true current state.
                return true;
            }
        }

        /// <summary>
        /// Ensures posters are always stored as a TMDb-relative path (e.g. "/abc123.jpg"),
        /// never a full "https://image.tmdb.org/t/p/{size}/..." URL, so callers can
        /// consistently prefix the CDN base URL when rendering.
        /// </summary>
        private static string NormalizePosterPath(string? posterPath)
        {
            if (string.IsNullOrWhiteSpace(posterPath))
                return "";

            var fileName = posterPath[(posterPath.LastIndexOf('/') + 1)..];
            return string.IsNullOrEmpty(fileName) ? "" : "/" + fileName;
        }
    }
}
