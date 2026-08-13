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
            var user = await ResolveVisibleUserAsync(username, viewerId);
            if (user == null)
                return null;

            // Rewatch'lar da çekiliyor: süre ve yıllık dağılım gerçekten harcanan
            // zamanı göstermeli. "Kaç bölüm izledin" ise tekil bölüm sayar.
            var watched = await _context.WatchedEpisodes
                .Where(w => w.UserId == user.Id)
                .Select(w => new { w.WatchedAt, w.Episode!.Runtime, w.RewatchNo })
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
                WatchedEpisodeCount = watched.Count(w => w.RewatchNo == 0),
                RewatchCount = watched.Count(w => w.RewatchNo > 0),
                ShowsWatchingCount = statuses.Count(s => s == WatchStatus.Watching),
                ShowsCompletedCount = statuses.Count(s => s == WatchStatus.Completed),
                ShowCount = statuses.Count,
                ReviewCount = await _context.Reviews.CountAsync(r => r.UserId == user.Id),
                RatingCount = await _context.Ratings.CountAsync(r => r.UserId == user.Id),
                AverageRating = showRatings.Count == 0 ? null : (double)showRatings.Average(),
                // Süresi bilinmeyen bölümler toplama girmez; tahmin yürütmüyoruz.
                // Yeniden izlemeler girer: bir bölümü üç kez izlemek üç kat zaman.
                TotalMinutes = watched.Sum(w => w.Runtime ?? 0),
                FavoriteShows = favorites,
                YearlyCounts = watched
                    .GroupBy(w => w.WatchedAt.Year)
                    .OrderBy(g => g.Key)
                    .Select(g => new YearlyCountDto { Year = g.Key, EpisodeCount = g.Count() })
                    .ToList()
            };
        }

        public async Task<UserStatsDetailDto?> GetDetailedStatsAsync(string username, string? viewerId)
        {
            var user = await ResolveVisibleUserAsync(username, viewerId);
            if (user == null)
                return null;

            // İzlenen bölümler, dizisi ve süresiyle birlikte tek sorguda; tüm
            // kırılımlar (yıl, tür, dizi) bu tek küme üzerinden bellekte çıkarılıyor.
            // Rewatch satırları da geliyor: süre ve yıllık dağılım harcanan zamanı
            // gösterir, bölüm sayıları ise RewatchNo == 0 ile tekilleştirilir.
            var watched = await _context.WatchedEpisodes
                .Where(w => w.UserId == user.Id)
                .Select(w => new
                {
                    w.WatchedAt,
                    w.Episode!.Runtime,
                    w.RewatchNo,
                    ShowId = w.Episode.Season!.ShowId,
                    TmdbId = w.Episode.Season.Show!.TmdbId,
                    ShowName = w.Episode.Season.Show.Name,
                    w.Episode.Season.Show.PosterPath
                })
                .ToListAsync();

            var firstWatches = watched.Where(w => w.RewatchNo == 0).ToList();

            var userShows = await _context.UserShows
                .Where(us => us.UserId == user.Id)
                .Select(us => new
                {
                    us.ShowId,
                    us.Status,
                    GenreIds = us.Show!.Genres.Select(g => g.Id).ToList()
                })
                .ToListAsync();

            var genreNames = await _context.Genres.ToDictionaryAsync(g => g.Id, g => g.Name);

            var ratings = await _context.Ratings
                .Where(r => r.UserId == user.Id)
                .Select(r => new { r.TargetType, r.Value })
                .ToListAsync();

            var showRatings = ratings.Where(r => r.TargetType == RatingTargetType.Show).ToList();

            // Türe göre bölüm sayısı: dizinin türleri o dizinin tüm izlenen
            // bölümlerine sayılır, yani bir bölüm birden çok türe girebilir.
            var episodesByShow = watched
                .GroupBy(w => w.ShowId)
                .ToDictionary(g => g.Key, g => g.Count());

            var genreStats = userShows
                .SelectMany(us => us.GenreIds.Select(genreId => new { genreId, us.ShowId }))
                .GroupBy(x => x.genreId)
                .Select(g => new GenreStatDto
                {
                    GenreId = g.Key,
                    Name = genreNames.TryGetValue(g.Key, out var name) ? name : "?",
                    ShowCount = g.Select(x => x.ShowId).Distinct().Count(),
                    EpisodeCount = g.Select(x => x.ShowId).Distinct()
                        .Sum(showId => episodesByShow.TryGetValue(showId, out var count) ? count : 0)
                })
                .OrderByDescending(g => g.EpisodeCount)
                .ThenByDescending(g => g.ShowCount)
                .ToList();

            return new UserStatsDetailDto
            {
                Username = user.UserName ?? string.Empty,
                DisplayName = string.IsNullOrWhiteSpace(user.DisplayName)
                    ? user.UserName ?? string.Empty
                    : user.DisplayName,

                WatchedEpisodeCount = firstWatches.Count,
                RewatchCount = watched.Count - firstWatches.Count,
                TotalMinutes = watched.Sum(w => w.Runtime ?? 0),
                EpisodesWithoutRuntime = watched.Count(w => w.Runtime == null),

                ShowCount = userShows.Count,
                ShowsWatchingCount = userShows.Count(us => us.Status == WatchStatus.Watching),
                ShowsCompletedCount = userShows.Count(us => us.Status == WatchStatus.Completed),
                ShowsDroppedCount = userShows.Count(us => us.Status == WatchStatus.Dropped),
                ShowsPlannedCount = userShows.Count(us => us.Status == WatchStatus.PlanToWatch),
                ShowsOnHoldCount = userShows.Count(us => us.Status == WatchStatus.OnHold),

                ReviewCount = await _context.Reviews.CountAsync(r => r.UserId == user.Id),
                RatingCount = ratings.Count,
                AverageRating = showRatings.Count == 0 ? null : (double)showRatings.Average(r => r.Value),

                Yearly = watched
                    .GroupBy(w => w.WatchedAt.Year)
                    .OrderBy(g => g.Key)
                    .Select(g => new YearlyStatDto
                    {
                        Year = g.Key,
                        EpisodeCount = g.Count(),
                        Minutes = g.Sum(w => w.Runtime ?? 0)
                    })
                    .ToList(),

                Genres = genreStats,

                TopShows = watched
                    .GroupBy(w => w.ShowId)
                    .Select(g => new TopShowDto
                    {
                        TmdbId = g.First().TmdbId,
                        Name = g.First().ShowName,
                        PosterPath = g.First().PosterPath,
                        EpisodeCount = g.Count(),
                        Minutes = g.Sum(w => w.Runtime ?? 0)
                    })
                    .OrderByDescending(s => s.EpisodeCount)
                    .Take(10)
                    .ToList(),

                RatingDistribution = BuildRatingDistribution(ratings.Select(r => r.Value))
            };
        }

        /// <summary>Yarım yıldızlık on kova; hiç puan almayanlar da 0 ile döner.</summary>
        private static List<RatingBucketDto> BuildRatingDistribution(IEnumerable<decimal> values)
        {
            var counts = values.GroupBy(v => v).ToDictionary(g => g.Key, g => g.Count());

            return Enumerable.Range(1, 10)
                .Select(step => new decimal(step) / 2)
                .Select(value => new RatingBucketDto
                {
                    Value = value,
                    Count = counts.TryGetValue(value, out var count) ? count : 0
                })
                .ToList();
        }

        /// <summary>
        /// Gizli profil yalnızca sahibine, engelli taraflar birbirine hiç görünmez
        /// (bkz. FollowService'teki aynı kural).
        /// </summary>
        private async Task<AppUser?> ResolveVisibleUserAsync(string username, string? viewerId)
        {
            var normalized = username.ToUpperInvariant();
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.NormalizedUserName == normalized || u.UserName == username);

            if (user == null || (user.IsPrivate && user.Id != viewerId))
                return null;

            if (await _context.IsBlockedBetweenAsync(viewerId, user.Id))
                return null;

            return user;
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
