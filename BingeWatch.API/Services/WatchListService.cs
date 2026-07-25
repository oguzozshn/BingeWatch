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
            Console.WriteLine($"[Add] userId={userId}, seriesId={series?.Id}, name={series?.Name}");

            try
            {
                var existingItem = await _context.WatchListItems
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.SeriesId == series.Id);

                if (existingItem != null)
                {
                    Console.WriteLine("[Add] Zaten var, eklenmedi.");
                    return false;
                }

                var watchListItem = new WatchListItem
                {
                    SeriesId = series.Id,
                    SeriesName = series.Name ?? "",
                    Overview = series.Overview ?? "",
                    PosterPath = series.PosterPath ?? "",
                    FirstAirDate = series.FirstAirDate,
                    UserId = userId,
                    AddedDate = DateTime.UtcNow
                };

                Console.WriteLine("[Add] DB'ye kaydediliyor...");
                _context.WatchListItems.Add(watchListItem);
                await _context.SaveChangesAsync();
                Console.WriteLine("[Add] Başarıyla eklendi!");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Add] ERROR: {ex.Message}");
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

        public async Task<bool> ToggleAsync(string userId, SeriesDto series)
        {
            Console.WriteLine($"[Toggle] userId={userId}, seriesId={series?.Id}, name={series?.Name}");

            var existing = await _context.WatchListItems
                .FirstOrDefaultAsync(x => x.UserId == userId && x.SeriesId == series.Id);

            Console.WriteLine(existing == null
                ? "[Toggle] Watchlist'te daha önce yok, eklemeye geçiliyor."
                : "[Toggle] Watchlist'te bulundu, kaldırılacak.");

            if (existing == null)
            {
                var added = await AddToWatchListAsync(userId, series);
                Console.WriteLine($"[Toggle] AddToWatchListAsync sonucu = {added}");
                return added;
            }

            try
            {
                _context.WatchListItems.Remove(existing);
                await _context.SaveChangesAsync();
                Console.WriteLine("[Toggle] Başarıyla kaldırıldı.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Toggle] Remove hatası: {ex.Message}");
            }

            return false;
        }


    }
} 