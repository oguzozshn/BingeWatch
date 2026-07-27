using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BingeWatch.API.Services
{
    public class ActivityService : IActivityService
    {
        /// <summary>Akış kartında gösterilen inceleme parçasının uzunluğu.</summary>
        private const int ExcerptLength = 240;

        private readonly BingeOnDbContext _context;

        public ActivityService(BingeOnDbContext context)
        {
            _context = context;
        }

        public async Task RecordRatedAsync(string userId, int showId, RatingTargetType targetType,
            int? seasonNumber, int? episodeId, decimal value)
        {
            var (season, episode) = Normalize(targetType, seasonNumber, episodeId);

            var existing = await FindRatedAsync(userId, showId, season, episode);
            if (existing == null)
            {
                _context.ActivityEvents.Add(new ActivityEvent
                {
                    UserId = userId,
                    Type = ActivityType.Rated,
                    ShowId = showId,
                    SeasonNumber = season,
                    EpisodeId = episode,
                    RatingValue = value
                });
            }
            else
            {
                // Puanını değiştirmek yeni bir olay değil; mevcut olay güncellenip başa taşınır.
                existing.RatingValue = value;
                existing.CreatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        public async Task RemoveRatedAsync(string userId, int showId, RatingTargetType targetType,
            int? seasonNumber, int? episodeId)
        {
            var (season, episode) = Normalize(targetType, seasonNumber, episodeId);

            var existing = await FindRatedAsync(userId, showId, season, episode);
            if (existing == null)
                return;

            _context.ActivityEvents.Remove(existing);
            await _context.SaveChangesAsync();
        }

        public async Task RecordReviewedAsync(string userId, int reviewId, int showId, int? seasonNumber)
        {
            var existing = await _context.ActivityEvents.FirstOrDefaultAsync(a =>
                a.Type == ActivityType.Reviewed && a.ReviewId == reviewId);

            if (existing == null)
            {
                _context.ActivityEvents.Add(new ActivityEvent
                {
                    UserId = userId,
                    Type = ActivityType.Reviewed,
                    ShowId = showId,
                    SeasonNumber = seasonNumber,
                    ReviewId = reviewId
                });
            }
            else
            {
                existing.CreatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        public async Task RemoveReviewedAsync(int reviewId)
        {
            var events = await _context.ActivityEvents
                .Where(a => a.Type == ActivityType.Reviewed && a.ReviewId == reviewId)
                .ToListAsync();
            if (events.Count == 0)
                return;

            _context.ActivityEvents.RemoveRange(events);
            await _context.SaveChangesAsync();
        }

        public async Task RecordWatchedAsync(string userId, int showId, int lastEpisodeId, int episodeCount)
        {
            if (episodeCount <= 0)
                return;

            _context.ActivityEvents.Add(new ActivityEvent
            {
                UserId = userId,
                Type = ActivityType.Watched,
                ShowId = showId,
                EpisodeId = lastEpisodeId,
                EpisodeCount = episodeCount
            });

            await _context.SaveChangesAsync();
        }

        public async Task RemoveWatchedAsync(string userId, IReadOnlyCollection<int> episodeIds)
        {
            if (episodeIds.Count == 0)
                return;

            // Toplu izleme tek olayla temsil edildiği için yalnızca olayın "son bölüm"ü
            // eşleşenler silinir; aradan tek bölüm kaldırmak olayı bozmaz.
            var events = await _context.ActivityEvents
                .Where(a => a.UserId == userId && a.Type == ActivityType.Watched
                            && a.EpisodeId != null && episodeIds.Contains(a.EpisodeId.Value))
                .ToListAsync();
            if (events.Count == 0)
                return;

            _context.ActivityEvents.RemoveRange(events);
            await _context.SaveChangesAsync();
        }

        public async Task RecordFollowedAsync(string followerId, string followeeId)
        {
            var exists = await _context.ActivityEvents.AnyAsync(a =>
                a.UserId == followerId && a.Type == ActivityType.Followed && a.TargetUserId == followeeId);
            if (exists)
                return;

            _context.ActivityEvents.Add(new ActivityEvent
            {
                UserId = followerId,
                Type = ActivityType.Followed,
                TargetUserId = followeeId
            });

            await _context.SaveChangesAsync();
        }

        public async Task RemoveFollowedAsync(string followerId, string followeeId)
        {
            var events = await _context.ActivityEvents
                .Where(a => a.UserId == followerId && a.Type == ActivityType.Followed
                            && a.TargetUserId == followeeId)
                .ToListAsync();
            if (events.Count == 0)
                return;

            _context.ActivityEvents.RemoveRange(events);
            await _context.SaveChangesAsync();
        }

        public async Task<PagedResult<ActivityDto>> GetFeedAsync(string viewerId, string? cursor, int take)
        {
            take = Math.Clamp(take, 1, 100);

            var followeeIds = await _context.Follows
                .Where(f => f.FollowerId == viewerId)
                .Select(f => f.FolloweeId)
                .ToListAsync();

            // Kendi olayları da akışta: kimseyi takip etmeyen kullanıcı boş sayfa görmesin.
            followeeIds.Add(viewerId);

            // Engel takipleri koparır ama "X, Y'yi takip etti" gibi olaylar üçüncü
            // kişiler üzerinden akışa sızabilir; engellenenler iki yönde de elenir.
            var hidden = await _context.HiddenUserIdsAsync(viewerId);

            var query = _context.ActivityEvents
                .Where(a => followeeIds.Contains(a.UserId))
                .Where(a => !hidden.Contains(a.UserId)
                         && (a.TargetUserId == null || !hidden.Contains(a.TargetUserId)));

            // İmleçten sonrası: aynı saniyeye düşen olaylar için id ikinci anahtar.
            var after = Cursor.DecodeKeyset(cursor);
            if (after != null)
            {
                query = query.Where(a => a.CreatedAt < after.Value.Timestamp
                                      || (a.CreatedAt == after.Value.Timestamp && a.Id < after.Value.Id));
            }

            var events = await query
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .Take(take)
                .Include(a => a.User)
                .Include(a => a.Show)
                .Include(a => a.Episode).ThenInclude(e => e!.Season)
                .Include(a => a.TargetUser)
                .ToListAsync();

            if (events.Count == 0)
                return PagedResult<ActivityDto>.Empty();

            var reviewIds = events.Where(a => a.ReviewId != null).Select(a => a.ReviewId!.Value).ToList();
            var reviews = reviewIds.Count == 0
                ? new Dictionary<int, Review>()
                : await _context.Reviews
                    .Where(r => reviewIds.Contains(r.Id))
                    .ToDictionaryAsync(r => r.Id);

            var items = events.Select(a =>
            {
                Review? review = null;
                if (a.ReviewId != null)
                    reviews.TryGetValue(a.ReviewId.Value, out review);

                return new ActivityDto
                {
                    Id = a.Id,
                    Type = a.Type,
                    CreatedAt = a.CreatedAt,
                    Username = a.User?.UserName ?? string.Empty,
                    DisplayName = string.IsNullOrWhiteSpace(a.User?.DisplayName)
                        ? a.User?.UserName ?? string.Empty
                        : a.User!.DisplayName,
                    AvatarUrl = a.User?.AvatarUrl,
                    TmdbShowId = a.Show?.TmdbId,
                    ShowName = a.Show?.Name,
                    ShowPosterPath = a.Show?.PosterPath,
                    // Bölüm hedefli olaylarda sezon numarası bölümün kendisinden gelir.
                    SeasonNumber = a.Episode?.Season?.SeasonNumber ?? a.SeasonNumber,
                    EpisodeNumber = a.Episode?.EpisodeNumber,
                    EpisodeName = a.Episode?.Name,
                    EpisodeCount = a.EpisodeCount,
                    RatingValue = a.RatingValue,
                    ReviewId = a.ReviewId,
                    ReviewExcerpt = Excerpt(review?.Body),
                    ReviewHasSpoilers = review?.HasSpoilers ?? false,
                    TargetUsername = a.TargetUser?.UserName,
                    TargetDisplayName = string.IsNullOrWhiteSpace(a.TargetUser?.DisplayName)
                        ? a.TargetUser?.UserName
                        : a.TargetUser!.DisplayName
                };
            }).ToList();

            var last = events[^1];

            return new PagedResult<ActivityDto>
            {
                Items = items,
                // Sayfa tam dolmadıysa liste bitmiştir; boşuna bir istek daha atılmasın.
                NextCursor = events.Count < take ? null : Cursor.EncodeKeyset(last.CreatedAt, last.Id)
            };
        }

        private Task<ActivityEvent?> FindRatedAsync(string userId, int showId, int? seasonNumber, int? episodeId) =>
            _context.ActivityEvents.FirstOrDefaultAsync(a =>
                a.UserId == userId && a.Type == ActivityType.Rated && a.ShowId == showId
                && a.SeasonNumber == seasonNumber && a.EpisodeId == episodeId);

        /// <summary>Hedef seviyesine uymayan alanları temizler; dizi puanında sezon/bölüm dolu kalmasın.</summary>
        private static (int? SeasonNumber, int? EpisodeId) Normalize(RatingTargetType targetType,
            int? seasonNumber, int? episodeId) => targetType switch
            {
                RatingTargetType.Season => (seasonNumber, null),
                RatingTargetType.Episode => (null, episodeId),
                _ => (null, null)
            };

        private static string? Excerpt(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;

            return body.Length <= ExcerptLength ? body : body[..ExcerptLength].TrimEnd() + "…";
        }
    }
}
