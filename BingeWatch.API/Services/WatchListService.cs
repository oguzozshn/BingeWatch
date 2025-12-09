using Microsoft.EntityFrameworkCore;
using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;

namespace BingeWatch.API.Services
{
    public class WatchListService : IWatchListService
    {
        private readonly BingeOnDbContext _context;

        public WatchListService(BingeOnDbContext context)
        {
            _context = context;
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
            try
            {
                // Aynı dizi zaten var mı kontrol et
                var existingItem = await _context.WatchListItems
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.SeriesId == series.Id);

                if (existingItem != null)
                {
                    return false; // Zaten ekli
                }

                var watchListItem = new WatchListItem
                {
                    SeriesId = series.Id,
                    SeriesName = series.Name,
                    Overview = series.Overview ?? string.Empty,
                    PosterPath = series.PosterPath ?? string.Empty,
                    FirstAirDate = series.FirstAirDate,
                    UserId = userId,
                    AddedDate = DateTime.UtcNow
                };

                _context.WatchListItems.Add(watchListItem);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error adding to watchlist: {ex.Message}");
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
                Console.WriteLine($"Error removing from watchlist: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> IsInWatchListAsync(string userId, int seriesId)
        {
            return await _context.WatchListItems
                .AnyAsync(w => w.UserId == userId && w.SeriesId == seriesId);
        }

        public async Task<bool> ToggleAsync(string userId, int seriesId)
        {
            var existing = await _context.WatchListItems
           .FirstOrDefaultAsync(x => x.UserId == userId && x.SeriesId == seriesId);

            if (existing == null)
            {
                return false;
            }

            _context.WatchListItems.Remove(existing);
            await _context.SaveChangesAsync();

            return false;
        }
    }
} 