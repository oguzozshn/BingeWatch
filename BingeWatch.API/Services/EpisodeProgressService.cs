using Microsoft.EntityFrameworkCore;
using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;

namespace BingeWatch.API.Services
{
    public class EpisodeProgressService : IEpisodeProgressService
    {
        private readonly BingeOnDbContext _context;

        public EpisodeProgressService(BingeOnDbContext context)
        {
            _context = context;
        }

        public async Task<bool> SetEpisodeWatchedAsync(string userId, int episodeId, bool watched)
        {
            var episode = await _context.Episodes
                .Include(e => e.Season)
                .FirstOrDefaultAsync(e => e.Id == episodeId);
            if (episode == null)
                return false;

            await ApplyWatchedAsync(userId, new[] { episode }, watched);
            await UpdateShowStatusAsync(userId, episode.Season!.ShowId);
            return true;
        }

        public async Task<int> SetSeasonWatchedAsync(string userId, int showTmdbId, int seasonNumber, bool watched)
        {
            var show = await _context.Shows.FirstOrDefaultAsync(s => s.TmdbId == showTmdbId);
            if (show == null)
                return 0;

            var episodes = await _context.Episodes
                .Include(e => e.Season)
                .Where(e => e.Season!.ShowId == show.Id && e.Season.SeasonNumber == seasonNumber)
                .Where(e => e.AirDate == null || e.AirDate <= DateTime.UtcNow) // yayınlanmamışı işaretleme
                .ToListAsync();

            await ApplyWatchedAsync(userId, episodes, watched);
            await UpdateShowStatusAsync(userId, show.Id);
            return episodes.Count;
        }

        public async Task<int> SetWatchedUpToAsync(string userId, int showTmdbId, int episodeId)
        {
            var show = await _context.Shows.FirstOrDefaultAsync(s => s.TmdbId == showTmdbId);
            if (show == null)
                return 0;

            var target = await _context.Episodes.Include(e => e.Season)
                .FirstOrDefaultAsync(e => e.Id == episodeId && e.Season!.ShowId == show.Id);
            if (target == null)
                return 0;

            var allEpisodes = await _context.Episodes
                .Include(e => e.Season)
                .Where(e => e.Season!.ShowId == show.Id)
                .Where(e => e.AirDate == null || e.AirDate <= DateTime.UtcNow)
                .OrderBy(e => e.Season!.SeasonNumber).ThenBy(e => e.EpisodeNumber)
                .ToListAsync();

            var upTo = allEpisodes
                .TakeWhile(e => e.Season!.SeasonNumber < target.Season!.SeasonNumber
                             || (e.Season.SeasonNumber == target.Season.SeasonNumber && e.EpisodeNumber <= target.EpisodeNumber))
                .ToList();

            await ApplyWatchedAsync(userId, upTo, watched: true);
            await UpdateShowStatusAsync(userId, show.Id);
            return upTo.Count;
        }

        public async Task<ShowProgressDto?> GetShowProgressAsync(string userId, int showTmdbId)
        {
            var show = await _context.Shows.FirstOrDefaultAsync(s => s.TmdbId == showTmdbId);
            if (show == null)
                return null;

            var userShow = await _context.UserShows
                .FirstOrDefaultAsync(us => us.UserId == userId && us.ShowId == show.Id);

            var airedEpisodes = await _context.Episodes
                .Include(e => e.Season)
                .Where(e => e.Season!.ShowId == show.Id)
                .Where(e => e.AirDate == null || e.AirDate <= DateTime.UtcNow)
                .OrderBy(e => e.Season!.SeasonNumber).ThenBy(e => e.EpisodeNumber)
                .ToListAsync();

            var watchedIds = await _context.WatchedEpisodes
                .Where(w => w.UserId == userId && w.RewatchNo == 0
                            && airedEpisodes.Select(e => e.Id).Contains(w.EpisodeId))
                .Select(w => w.EpisodeId)
                .ToListAsync();

            var nextEpisode = airedEpisodes.FirstOrDefault(e => !watchedIds.Contains(e.Id));

            return new ShowProgressDto
            {
                TmdbId = show.TmdbId,
                Status = userShow?.Status.ToString() ?? WatchStatus.PlanToWatch.ToString(),
                TotalEpisodes = airedEpisodes.Count,
                WatchedEpisodes = watchedIds.Count,
                NextEpisode = nextEpisode == null ? null : new NextEpisodeDto
                {
                    TmdbShowId = show.TmdbId,
                    ShowName = show.Name,
                    ShowPosterPath = show.PosterPath,
                    SeasonNumber = nextEpisode.Season!.SeasonNumber,
                    EpisodeNumber = nextEpisode.EpisodeNumber,
                    EpisodeName = nextEpisode.Name,
                    AirDate = nextEpisode.AirDate,
                    IsUnaired = false
                }
            };
        }

        public async Task<HashSet<int>> GetWatchedEpisodeIdsAsync(string userId, int showTmdbId)
        {
            var show = await _context.Shows.FirstOrDefaultAsync(s => s.TmdbId == showTmdbId);
            if (show == null)
                return new HashSet<int>();

            var episodeIds = await _context.Episodes
                .Where(e => e.Season!.ShowId == show.Id)
                .Select(e => e.Id)
                .ToListAsync();

            var watched = await _context.WatchedEpisodes
                .Where(w => w.UserId == userId && w.RewatchNo == 0 && episodeIds.Contains(w.EpisodeId))
                .Select(w => w.EpisodeId)
                .ToListAsync();

            return watched.ToHashSet();
        }

        public async Task<List<NextEpisodeDto>> GetNextUpAsync(string userId)
        {
            var activeShowIds = await _context.UserShows
                .Where(us => us.UserId == userId
                             && us.Status != WatchStatus.Completed
                             && us.Status != WatchStatus.Dropped)
                .Select(us => us.ShowId)
                .ToListAsync();

            var result = new List<NextEpisodeDto>();

            foreach (var showId in activeShowIds)
            {
                var show = await _context.Shows.FirstAsync(s => s.Id == showId);

                var episodes = await _context.Episodes
                    .Include(e => e.Season)
                    .Where(e => e.Season!.ShowId == showId)
                    .OrderBy(e => e.Season!.SeasonNumber).ThenBy(e => e.EpisodeNumber)
                    .ToListAsync();

                var watchedIds = (await _context.WatchedEpisodes
                        .Where(w => w.UserId == userId && w.RewatchNo == 0)
                        .Select(w => w.EpisodeId)
                        .ToListAsync())
                    .ToHashSet();

                var next = episodes.FirstOrDefault(e => !watchedIds.Contains(e.Id));
                if (next == null)
                    continue; // her şeyi izlemiş

                result.Add(new NextEpisodeDto
                {
                    TmdbShowId = show.TmdbId,
                    ShowName = show.Name,
                    ShowPosterPath = show.PosterPath,
                    SeasonNumber = next.Season!.SeasonNumber,
                    EpisodeNumber = next.EpisodeNumber,
                    EpisodeName = next.Name,
                    AirDate = next.AirDate,
                    IsUnaired = next.AirDate.HasValue && next.AirDate.Value.Date > DateTime.UtcNow.Date
                });
            }

            return result
                .OrderBy(r => r.IsUnaired)
                .ThenBy(r => r.AirDate ?? DateTime.MaxValue)
                .ToList();
        }

        public async Task<List<UpcomingEpisodeDto>> GetUpcomingEpisodesAsync(string userId, int daysAhead)
        {
            var cutoff = DateTime.UtcNow.AddDays(daysAhead);

            var showIds = await _context.UserShows
                .Where(us => us.UserId == userId && us.Status != WatchStatus.Dropped)
                .Select(us => us.ShowId)
                .ToListAsync();

            return await _context.Episodes
                .Include(e => e.Season).ThenInclude(s => s!.Show)
                .Where(e => showIds.Contains(e.Season!.ShowId))
                .Where(e => e.AirDate != null && e.AirDate > DateTime.UtcNow && e.AirDate <= cutoff)
                .OrderBy(e => e.AirDate)
                .Select(e => new UpcomingEpisodeDto
                {
                    TmdbShowId = e.Season!.Show!.TmdbId,
                    ShowName = e.Season.Show.Name,
                    ShowPosterPath = e.Season.Show.PosterPath,
                    SeasonNumber = e.Season.SeasonNumber,
                    EpisodeNumber = e.EpisodeNumber,
                    EpisodeName = e.Name,
                    AirDate = e.AirDate!.Value
                })
                .ToListAsync();
        }

        private async Task ApplyWatchedAsync(string userId, IReadOnlyCollection<Episode> episodes, bool watched)
        {
            if (episodes.Count == 0)
                return;

            var episodeIds = episodes.Select(e => e.Id).ToList();

            var existing = await _context.WatchedEpisodes
                .Where(w => w.UserId == userId && w.RewatchNo == 0 && episodeIds.Contains(w.EpisodeId))
                .ToListAsync();

            if (watched)
            {
                var existingIds = existing.Select(w => w.EpisodeId).ToHashSet();
                foreach (var id in episodeIds.Where(id => !existingIds.Contains(id)))
                {
                    _context.WatchedEpisodes.Add(new WatchedEpisode
                    {
                        UserId = userId,
                        EpisodeId = id,
                        WatchedAt = DateTime.UtcNow,
                        RewatchNo = 0
                    });
                }
            }
            else
            {
                _context.WatchedEpisodes.RemoveRange(existing);
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Bir bölüm işaretlendikten sonra dizinin durumunu günceller: ilk işaretlemede
        /// "İzliyorum"a geçer, tüm yayınlanmış bölümler izlenince "Bitirdim" olur.
        /// Kullanıcı diziyi listesine hiç eklemediyse (yalnızca bölüm işaretlediyse)
        /// dokunmadan çıkar.
        /// </summary>
        private async Task UpdateShowStatusAsync(string userId, int showId)
        {
            var userShow = await _context.UserShows
                .FirstOrDefaultAsync(us => us.UserId == userId && us.ShowId == showId);
            if (userShow == null)
                return;

            var airedCount = await _context.Episodes
                .Where(e => e.Season!.ShowId == showId && (e.AirDate == null || e.AirDate <= DateTime.UtcNow))
                .CountAsync();

            var watchedCount = await _context.WatchedEpisodes
                .Where(w => w.UserId == userId && w.RewatchNo == 0)
                .Join(_context.Episodes.Where(e => e.Season!.ShowId == showId),
                      w => w.EpisodeId, e => e.Id, (w, e) => w.Id)
                .CountAsync();

            // Kullanıcının bilerek "Bıraktım" / "Ertelendi" seçtiği bir diziyi bölüm
            // işaretlemesi otomatik olarak geri "İzliyorum"a çekmemeli.
            if (userShow.Status == WatchStatus.Dropped || userShow.Status == WatchStatus.OnHold)
                return;

            if (airedCount > 0 && watchedCount >= airedCount)
            {
                userShow.Status = WatchStatus.Completed;
                userShow.CompletedAt ??= DateTime.UtcNow;
            }
            else if (watchedCount > 0)
            {
                // Hem "henüz başlamadım"dan ilerlemeyi hem de bir bölümün
                // işareti kaldırılınca "Bitirdim"den geri düşmeyi kapsar.
                userShow.Status = WatchStatus.Watching;
                userShow.StartedAt ??= DateTime.UtcNow;
                userShow.CompletedAt = null;
            }
            else
            {
                userShow.Status = WatchStatus.PlanToWatch;
                userShow.CompletedAt = null;
            }

            await _context.SaveChangesAsync();
        }
    }
}
