using Microsoft.EntityFrameworkCore;
using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;

namespace BingeWatch.API.Services
{
    public class EpisodeProgressService : IEpisodeProgressService
    {
        private readonly BingeOnDbContext _context;
        private readonly IActivityService _activityService;

        public EpisodeProgressService(BingeOnDbContext context, IActivityService activityService)
        {
            _context = context;
            _activityService = activityService;
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

        /// <summary>
        /// Yeniden izleme kaydı. İlerleme modeline bilerek dokunmuyor: durum
        /// geçişi tetiklenmiyor, "Sırada ne var" ve ilerleme çubuğu yalnızca
        /// <c>RewatchNo == 0</c> satırlarını okuduğu için etkilenmiyor. Yani
        /// bitmiş bir diziyi yeniden izlemek onu "İzliyorum"a geri düşürmez.
        /// </summary>
        public async Task<int?> AddRewatchAsync(string userId, int episodeId)
        {
            var records = await _context.WatchedEpisodes
                .Where(w => w.UserId == userId && w.EpisodeId == episodeId)
                .ToListAsync();

            // İlk izleme olmadan yeniden izleme olamaz; kapı burada.
            if (!records.Any(w => w.RewatchNo == 0))
                return null;

            var nextNo = records.Max(w => w.RewatchNo) + 1;

            _context.WatchedEpisodes.Add(new WatchedEpisode
            {
                UserId = userId,
                EpisodeId = episodeId,
                WatchedAt = DateTime.UtcNow,
                RewatchNo = nextNo
            });

            await _context.SaveChangesAsync();
            return nextNo;
        }

        public async Task<int?> RemoveLastRewatchAsync(string userId, int episodeId)
        {
            var last = await _context.WatchedEpisodes
                .Where(w => w.UserId == userId && w.EpisodeId == episodeId && w.RewatchNo > 0)
                .OrderByDescending(w => w.RewatchNo)
                .FirstOrDefaultAsync();

            if (last == null)
                return null;

            _context.WatchedEpisodes.Remove(last);
            await _context.SaveChangesAsync();

            return last.RewatchNo - 1;
        }

        public Task<int> GetRewatchCountAsync(string userId, int episodeId) =>
            _context.WatchedEpisodes
                .CountAsync(w => w.UserId == userId && w.EpisodeId == episodeId && w.RewatchNo > 0);

        /// <summary>
        /// Yarıda bırakma işareti. İzlenmiş bölüm reddediliyor: "izledim" ile
        /// "32. dakikada kaldım" birbirini dışlar, ikisi aynı anda doğru olamaz.
        /// </summary>
        public async Task<bool> SetBookmarkAsync(string userId, int episodeId, int positionMinutes)
        {
            if (positionMinutes < 0)
                return false;

            var episode = await _context.Episodes.FirstOrDefaultAsync(e => e.Id == episodeId);
            if (episode == null)
                return false;

            // Süresi bilinen bölümde sınırı aşan dakika kabul edilmiyor; süresi
            // bilinmeyen bölümde doğrulayacak bir üst sınır yok.
            if (episode.Runtime.HasValue && positionMinutes > episode.Runtime.Value)
                return false;

            var alreadyWatched = await _context.WatchedEpisodes
                .AnyAsync(w => w.UserId == userId && w.EpisodeId == episodeId && w.RewatchNo == 0);
            if (alreadyWatched)
                return false;

            var bookmark = await _context.EpisodeBookmarks
                .FirstOrDefaultAsync(b => b.UserId == userId && b.EpisodeId == episodeId);

            if (bookmark == null)
            {
                _context.EpisodeBookmarks.Add(new EpisodeBookmark
                {
                    UserId = userId,
                    EpisodeId = episodeId,
                    PositionMinutes = positionMinutes,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                bookmark.PositionMinutes = positionMinutes;
                bookmark.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ClearBookmarkAsync(string userId, int episodeId)
        {
            var bookmark = await _context.EpisodeBookmarks
                .FirstOrDefaultAsync(b => b.UserId == userId && b.EpisodeId == episodeId);
            if (bookmark == null)
                return false;

            _context.EpisodeBookmarks.Remove(bookmark);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int?> GetBookmarkAsync(string userId, int episodeId) =>
            (await _context.EpisodeBookmarks
                .FirstOrDefaultAsync(b => b.UserId == userId && b.EpisodeId == episodeId))
                ?.PositionMinutes;

        /// <summary>
        /// Ana sayfadaki "Sırada ne var" paneli. Dizi sayısından bağımsız olarak
        /// üç sorgu atar: diziler, o dizilerin bölümleri ve kullanıcının izledikleri.
        /// Gruplama bellekte yapılır — aktif dizi başına ayrı sorgu, en çok açılan
        /// sayfayı dizi sayısıyla doğru orantılı yavaşlatıyordu.
        /// </summary>
        public async Task<List<NextEpisodeDto>> GetNextUpAsync(string userId)
        {
            var activeShows = await _context.UserShows
                .Where(us => us.UserId == userId
                             && us.Status != WatchStatus.Completed
                             && us.Status != WatchStatus.Dropped)
                .Select(us => new
                {
                    us.ShowId,
                    us.Show!.TmdbId,
                    ShowName = us.Show.Name,
                    us.Show.PosterPath
                })
                .ToListAsync();

            if (activeShows.Count == 0)
                return new List<NextEpisodeDto>();

            var showIds = activeShows.Select(s => s.ShowId).ToList();

            var episodes = await _context.Episodes
                .Where(e => showIds.Contains(e.Season!.ShowId))
                .OrderBy(e => e.Season!.SeasonNumber).ThenBy(e => e.EpisodeNumber)
                .Select(e => new
                {
                    e.Id,
                    e.Season!.ShowId,
                    e.Season.SeasonNumber,
                    e.EpisodeNumber,
                    e.Name,
                    e.AirDate
                })
                .ToListAsync();

            var episodesByShow = episodes.GroupBy(e => e.ShowId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Kullanıcının izlediklerini bir kez çekiyoruz; bu küme diziye göre değişmiyor.
            var watchedIds = (await _context.WatchedEpisodes
                    .Where(w => w.UserId == userId && w.RewatchNo == 0)
                    .Select(w => w.EpisodeId)
                    .ToListAsync())
                .ToHashSet();

            // Yarıda bırakılanlar da tek sorguda; dizi sayısından bağımsız kalıyor.
            var bookmarks = await _context.EpisodeBookmarks
                .Where(b => b.UserId == userId)
                .ToDictionaryAsync(b => b.EpisodeId, b => b.PositionMinutes);

            var today = DateTime.UtcNow.Date;
            var result = new List<NextEpisodeDto>();

            foreach (var show in activeShows)
            {
                if (!episodesByShow.TryGetValue(show.ShowId, out var showEpisodes))
                    continue;

                // Yarıda bırakılan bölüm sıradakinin önüne geçer: "devam et",
                // "yeni bölüme başla"dan daha güçlü bir sinyal. Kullanıcı diziyi
                // ileriden işaretlemişse yarım bölüm sırada olmayabilir.
                var resuming = showEpisodes.FirstOrDefault(e => bookmarks.ContainsKey(e.Id));
                var next = resuming ?? showEpisodes.FirstOrDefault(e => !watchedIds.Contains(e.Id));
                if (next == null)
                    continue; // her şeyi izlemiş

                result.Add(new NextEpisodeDto
                {
                    TmdbShowId = show.TmdbId,
                    ShowName = show.ShowName,
                    ShowPosterPath = show.PosterPath,
                    SeasonNumber = next.SeasonNumber,
                    EpisodeNumber = next.EpisodeNumber,
                    EpisodeName = next.Name,
                    AirDate = next.AirDate,
                    IsUnaired = next.AirDate.HasValue && next.AirDate.Value.Date > today,
                    ResumeAtMinutes = bookmarks.TryGetValue(next.Id, out var minutes) ? minutes : null
                });
            }

            return result
                // Yarıda kalanlar en üstte: elindeki iş, başlanmamış işin önünde.
                .OrderBy(r => r.ResumeAtMinutes == null)
                .ThenBy(r => r.IsUnaired)
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
                var newlyWatched = episodes.Where(e => !existingIds.Contains(e.Id)).ToList();

                foreach (var episode in newlyWatched)
                {
                    _context.WatchedEpisodes.Add(new WatchedEpisode
                    {
                        UserId = userId,
                        EpisodeId = episode.Id,
                        WatchedAt = DateTime.UtcNow,
                        RewatchNo = 0
                    });
                }

                // Bölüm bitince "nerede kaldım" işareti anlamını yitirir.
                // Toplu işaretlemede araya giren yarım bölümler de temizlenir.
                var staleBookmarks = await _context.EpisodeBookmarks
                    .Where(b => b.UserId == userId && episodeIds.Contains(b.EpisodeId))
                    .ToListAsync();
                _context.EpisodeBookmarks.RemoveRange(staleBookmarks);

                await _context.SaveChangesAsync();

                // Toplu işaretleme akışta tek satır olsun: son bölüm + kaç bölüm.
                var last = newlyWatched
                    .OrderBy(e => e.Season!.SeasonNumber).ThenBy(e => e.EpisodeNumber)
                    .LastOrDefault();
                if (last != null)
                {
                    await _activityService.RecordWatchedAsync(userId, last.Season!.ShowId,
                        last.Id, newlyWatched.Count);
                }

                return;
            }

            // İşaret kaldırılırken yeniden izlemeler de gider: "bu bölümü
            // izlemedim" demek yalnızca ilk geçişi değil tümünü kapsar. Kalsalardı
            // izlenmemiş görünen bir bölüm istatistikte süre saymaya devam ederdi.
            var rewatches = await _context.WatchedEpisodes
                .Where(w => w.UserId == userId && w.RewatchNo > 0 && episodeIds.Contains(w.EpisodeId))
                .ToListAsync();

            _context.WatchedEpisodes.RemoveRange(existing);
            _context.WatchedEpisodes.RemoveRange(rewatches);
            await _context.SaveChangesAsync();

            await _activityService.RemoveWatchedAsync(userId, episodeIds);
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
