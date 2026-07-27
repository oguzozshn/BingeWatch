using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BingeWatch.API.Services
{
    public class UserStatsService : IUserStatsService
    {
        private readonly BingeOnDbContext _context;

        public UserStatsService(BingeOnDbContext context)
        {
            _context = context;
        }

        public async Task<UserStatsDto?> GetStatsAsync(string username, string? viewerId)
        {
            var normalized = username.ToUpperInvariant();
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.NormalizedUserName == normalized || u.UserName == username);

            if (user == null || (user.IsPrivate && user.Id != viewerId))
                return null;

            var watched = await _context.WatchedEpisodes
                .Where(w => w.UserId == user.Id && w.RewatchNo == 0)
                .Select(w => new { w.WatchedAt, w.Episode!.Runtime })
                .ToListAsync();

            var statuses = await _context.UserShows
                .Where(us => us.UserId == user.Id)
                .Select(us => us.Status)
                .ToListAsync();

            var showRatings = await _context.Ratings
                .Where(r => r.UserId == user.Id && r.TargetType == RatingTargetType.Show)
                .Select(r => r.Value)
                .ToListAsync();

            var favorites = await _context.UserShows
                .Where(us => us.UserId == user.Id && us.IsFavorite)
                .OrderBy(us => us.AddedAt)
                .Select(us => new FavoriteShowDto
                {
                    TmdbId = us.Show!.TmdbId,
                    Name = us.Show.Name,
                    PosterPath = us.Show.PosterPath
                })
                .ToListAsync();

            return new UserStatsDto
            {
                Username = user.UserName ?? string.Empty,
                WatchedEpisodeCount = watched.Count,
                ShowsWatchingCount = statuses.Count(s => s == WatchStatus.Watching),
                ShowsCompletedCount = statuses.Count(s => s == WatchStatus.Completed),
                ReviewCount = await _context.Reviews.CountAsync(r => r.UserId == user.Id),
                RatingCount = await _context.Ratings.CountAsync(r => r.UserId == user.Id),
                AverageRating = showRatings.Count == 0 ? null : (double)showRatings.Average(),
                // Süresi bilinmeyen bölümler toplama girmez; tahmin yürütmüyoruz.
                TotalMinutes = watched.Sum(w => w.Runtime ?? 0),
                FavoriteShows = favorites,
                YearlyCounts = watched
                    .GroupBy(w => w.WatchedAt.Year)
                    .OrderBy(g => g.Key)
                    .Select(g => new YearlyCountDto { Year = g.Key, EpisodeCount = g.Count() })
                    .ToList()
            };
        }

        public async Task<bool> SetFavoriteAsync(string userId, int showTmdbId, bool isFavorite)
        {
            var userShow = await _context.UserShows
                .FirstOrDefaultAsync(us => us.UserId == userId && us.Show!.TmdbId == showTmdbId);
            if (userShow == null)
                return false;

            userShow.IsFavorite = isFavorite;
            await _context.SaveChangesAsync();
            return true;
        }

        public Task<bool> IsFavoriteAsync(string userId, int showTmdbId) =>
            _context.UserShows.AnyAsync(us =>
                us.UserId == userId && us.Show!.TmdbId == showTmdbId && us.IsFavorite);
    }
}
