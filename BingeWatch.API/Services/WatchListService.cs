using Microsoft.EntityFrameworkCore;
using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;

namespace BingeWatch.API.Services
{
    /// <summary>
    /// Kullanıcının dizi listesi. Liste artık <see cref="UserShow"/> üzerinden tutuluyor;
    /// dizi bilgisi tek bir <see cref="Show"/> satırında paylaşılıyor (eskiden her
    /// kullanıcı için ayrı kopyalanıyordu).
    /// </summary>
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
            return await _context.UserShows
                .Where(us => us.UserId == userId)
                .OrderByDescending(us => us.AddedAt)
                .Select(us => new SeriesDto
                {
                    Id = us.Show!.TmdbId,
                    Name = us.Show.Name,
                    Overview = us.Show.Overview,
                    PosterPath = us.Show.PosterPath ?? "",
                    FirstAirDate = us.Show.FirstAirDate,
                    ImdbId = us.Show.ImdbId
                })
                .ToListAsync();
        }

        public async Task<bool> AddToWatchListAsync(string userId, SeriesDto series)
        {
            if (series == null)
                return false;

            _logger.LogInformation("Adding to watchlist: userId={UserId}, tmdbId={TmdbId}, name={Name}",
                userId, series.Id, series.Name);

            try
            {
                var show = await EnsureShowAsync(series);

                var exists = await _context.UserShows
                    .AnyAsync(us => us.UserId == userId && us.ShowId == show.Id);

                if (exists)
                {
                    _logger.LogInformation("Show {TmdbId} already in watchlist for user {UserId}", series.Id, userId);
                    return false;
                }

                _context.UserShows.Add(new UserShow
                {
                    UserId = userId,
                    ShowId = show.Id,
                    Status = WatchStatus.PlanToWatch,
                    AddedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                _logger.LogInformation("Show {TmdbId} added to watchlist for user {UserId}", series.Id, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding show {TmdbId} to watchlist for user {UserId}", series.Id, userId);
                return false;
            }
        }

        public async Task<bool> RemoveFromWatchListAsync(string userId, int tmdbShowId)
        {
            try
            {
                var userShow = await _context.UserShows
                    .FirstOrDefaultAsync(us => us.UserId == userId && us.Show!.TmdbId == tmdbShowId);

                if (userShow == null)
                    return false;

                _context.UserShows.Remove(userShow);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing show {TmdbId} from watchlist for user {UserId}", tmdbShowId, userId);
                return false;
            }
        }

        public async Task<bool> IsInWatchListAsync(string userId, int tmdbShowId)
        {
            return await _context.UserShows
                .AnyAsync(us => us.UserId == userId && us.Show!.TmdbId == tmdbShowId);
        }

        /// <summary>Listede varsa çıkarır, yoksa ekler. Dönen değer <b>son</b> durumdur.</summary>
        public async Task<bool> ToggleAsync(string userId, SeriesDto series)
        {
            if (series == null)
                return false;

            _logger.LogInformation("Toggling watchlist: userId={UserId}, tmdbId={TmdbId}", userId, series.Id);

            var existing = await _context.UserShows
                .FirstOrDefaultAsync(us => us.UserId == userId && us.Show!.TmdbId == series.Id);

            if (existing == null)
                return await AddToWatchListAsync(userId, series);

            try
            {
                _context.UserShows.Remove(existing);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Show {TmdbId} removed from watchlist for user {UserId}", series.Id, userId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing show {TmdbId} during toggle for user {UserId}", series.Id, userId);
                // Silme başarısız — dizi hâlâ listede, gerçek durumu bildir.
                return true;
            }
        }

        /// <summary>
        /// Diziyi katalogda bulur; yoksa elimizdeki özet bilgiden bir taslak satır açar.
        /// <c>LastSyncedAt</c> boş bırakılır: katalog servisi ilk erişimde TMDb'den
        /// sezon/bölüm verisiyle zenginleştirir.
        /// </summary>
        private async Task<Show> EnsureShowAsync(SeriesDto series)
        {
            var show = await _context.Shows.FirstOrDefaultAsync(s => s.TmdbId == series.Id);
            if (show != null)
                return show;

            show = new Show
            {
                TmdbId = series.Id,
                Name = series.Name ?? "",
                Overview = series.Overview ?? "",
                PosterPath = NormalizePosterPath(series.PosterPath),
                FirstAirDate = series.FirstAirDate,
                ImdbId = series.ImdbId,
                LastSyncedAt = default
            };

            _context.Shows.Add(show);
            await _context.SaveChangesAsync();

            return show;
        }

        /// <summary>
        /// Posterler her zaman TMDb'ye göreli yol olarak ("/abc123.jpg") saklanır,
        /// asla tam "https://image.tmdb.org/t/p/{size}/..." URL'i olarak değil; böylece
        /// çağıranlar CDN önekini tutarlı biçimde ekleyebilir.
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
