using Microsoft.EntityFrameworkCore;
using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;

namespace BingeWatch.API.Services
{
    public class ReviewService : IReviewService
    {
        private readonly BingeOnDbContext _context;
        private readonly IShowCatalogService _catalogService;
        private readonly IActivityService _activityService;
        private readonly INotificationService _notificationService;

        public ReviewService(BingeOnDbContext context, IShowCatalogService catalogService,
            IActivityService activityService, INotificationService notificationService)
        {
            _context = context;
            _catalogService = catalogService;
            _activityService = activityService;
            _notificationService = notificationService;
        }

        public async Task<ReviewDto?> UpsertAsync(string userId, int showTmdbId, UpsertReviewRequest request)
        {
            var body = request.Body?.Trim() ?? string.Empty;
            if (body.Length == 0)
                return null;

            // Kullanıcı diziyi hiç açmadan inceleme yazabilir; katalogda yoksa çekilir.
            var show = await _catalogService.GetOrSyncShowAsync(showTmdbId);
            if (show == null)
                return null;

            if (request.SeasonNumber != null &&
                !show.Seasons.Any(s => s.SeasonNumber == request.SeasonNumber))
                return null;

            var review = await _context.Reviews.FirstOrDefaultAsync(r =>
                r.UserId == userId && r.ShowId == show.Id && r.SeasonNumber == request.SeasonNumber);

            if (review == null)
            {
                review = new Review
                {
                    UserId = userId,
                    ShowId = show.Id,
                    SeasonNumber = request.SeasonNumber,
                    Body = body,
                    HasSpoilers = request.HasSpoilers
                };
                _context.Reviews.Add(review);
            }
            else
            {
                review.Body = body;
                review.HasSpoilers = request.HasSpoilers;
                review.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            await _activityService.RecordReviewedAsync(userId, review.Id, show.Id, request.SeasonNumber);

            return (await ProjectAsync(new[] { review.Id })).Single();
        }

        public async Task<bool> DeleteAsync(string userId, int reviewId)
        {
            var review = await _context.Reviews
                .FirstOrDefaultAsync(r => r.Id == reviewId && r.UserId == userId);
            if (review == null)
                return false;

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            await _activityService.RemoveReviewedAsync(reviewId);
            // Beğeni/yorum satırları FK cascade ile gitti; bildirimlerin FK'si yok, elle siliniyor.
            await _notificationService.RemoveForReviewAsync(reviewId);

            return true;
        }

        public async Task<List<ReviewDto>> GetForShowAsync(int showTmdbId, int? seasonNumber = null,
            string? viewerId = null)
        {
            var show = await _context.Shows.FirstOrDefaultAsync(s => s.TmdbId == showTmdbId);
            if (show == null)
                return new List<ReviewDto>();

            var hidden = await _context.HiddenUserIdsAsync(viewerId);

            var query = _context.Reviews.Where(r => r.ShowId == show.Id && !hidden.Contains(r.UserId));
            if (seasonNumber != null)
                query = query.Where(r => r.SeasonNumber == seasonNumber);

            var ids = await query
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => r.Id)
                .ToListAsync();

            return await ProjectAsync(ids, viewerId);
        }

        public async Task<List<ReviewDto>> GetOwnForShowAsync(string userId, int showTmdbId)
        {
            var show = await _context.Shows.FirstOrDefaultAsync(s => s.TmdbId == showTmdbId);
            if (show == null)
                return new List<ReviewDto>();

            var ids = await _context.Reviews
                .Where(r => r.ShowId == show.Id && r.UserId == userId)
                .OrderBy(r => r.SeasonNumber ?? -1)
                .Select(r => r.Id)
                .ToListAsync();

            return await ProjectAsync(ids, userId);
        }

        public async Task<PagedResult<ReviewDto>> GetFeedAsync(string? cursor, int take, ReviewSort sort,
            string? viewerId = null)
        {
            take = Math.Clamp(take, 1, 100);

            var hidden = await _context.HiddenUserIdsAsync(viewerId);
            var visible = _context.Reviews.Where(r => !hidden.Contains(r.UserId));

            // Puana göre sıralamada anahtar satırda durmuyor (ayrı Ratings tablosu),
            // keyset imleci kurulamıyor — o sıralama offset'te kalıyor. Zaman sıralı
            // ikisinde imleç (CreatedAt, Id) çiftini taşır.
            var offset = sort == ReviewSort.HighestRated ? Cursor.DecodeOffset(cursor) : 0;

            if (sort != ReviewSort.HighestRated)
            {
                var after = Cursor.DecodeKeyset(cursor);
                if (after != null)
                {
                    visible = sort == ReviewSort.Oldest
                        ? visible.Where(r => r.CreatedAt > after.Value.Timestamp
                                          || (r.CreatedAt == after.Value.Timestamp && r.Id > after.Value.Id))
                        : visible.Where(r => r.CreatedAt < after.Value.Timestamp
                                          || (r.CreatedAt == after.Value.Timestamp && r.Id < after.Value.Id));
                }
            }

            // Skip(0) bile SQL'e OFFSET yazdırıyor; keyset yolunda gereksiz.
            var ordered = Order(visible, sort);
            var paged = offset > 0 ? ordered.Skip(offset) : (IQueryable<Review>)ordered;

            var rows = await paged
                .Take(take)
                .Select(r => new { r.Id, r.CreatedAt })
                .ToListAsync();

            if (rows.Count == 0)
                return PagedResult<ReviewDto>.Empty();

            var items = await ProjectAsync(rows.Select(r => r.Id).ToList(), viewerId);

            string? nextCursor = null;
            if (rows.Count == take)
            {
                nextCursor = sort == ReviewSort.HighestRated
                    ? Cursor.EncodeOffset(offset + rows.Count)
                    : Cursor.EncodeKeyset(rows[^1].CreatedAt, rows[^1].Id);
            }

            return new PagedResult<ReviewDto> { Items = items, NextCursor = nextCursor };
        }

        /// <summary>
        /// Akış sıralaması. <see cref="ReviewSort.HighestRated"/> puana göre sıralanır
        /// ve puan ayrı tabloda durduğu için alt sorguyla çekilir — sayfayı çektikten
        /// sonra bellekte sıralamak yalnızca o sayfayı sıralar, "en yüksek puanlı"
        /// listesi ikinci sayfadan itibaren anlamını kaybederdi.
        /// Puansız incelemeler sona düşer (azalan sıralamada NULL en sonda).
        /// </summary>
        private IOrderedQueryable<Review> Order(IQueryable<Review> reviews, ReviewSort sort) => sort switch
        {
            ReviewSort.Oldest => reviews.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id),
            // Puan ayrı tabloda ve Rating.TargetId polimorfik: sezon hedefinde sezonun
            // yerel id'sine çevrilmesi gerekiyor. Alt sorgu buraya açık yazılmalı —
            // ayrı bir metoda alınırsa EF ifade ağacını çeviremiyor.
            ReviewSort.HighestRated => reviews
                .OrderByDescending(r => _context.Ratings
                    .Where(rt => rt.UserId == r.UserId)
                    .Where(rt => (r.SeasonNumber == null
                                  && rt.TargetType == RatingTargetType.Show
                                  && rt.TargetId == r.ShowId)
                              || (r.SeasonNumber != null
                                  && rt.TargetType == RatingTargetType.Season
                                  && _context.Seasons.Any(s => s.Id == rt.TargetId
                                                            && s.ShowId == r.ShowId
                                                            && s.SeasonNumber == r.SeasonNumber)))
                    .Select(rt => (decimal?)rt.Value)
                    .FirstOrDefault())
                .ThenByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id),
            _ => reviews.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
        };

        /// <summary>
        /// İnceleme id'lerini yazarı, dizisi ve yazarın aynı hedefe verdiği puanla
        /// birlikte DTO'ya çevirir. Sıra <paramref name="reviewIds"/>'in sırasıdır.
        /// </summary>
        private async Task<List<ReviewDto>> ProjectAsync(IReadOnlyList<int> reviewIds, string? viewerId = null)
        {
            if (reviewIds.Count == 0)
                return new List<ReviewDto>();

            var rows = await _context.Reviews
                .Where(r => reviewIds.Contains(r.Id))
                .Select(r => new
                {
                    Review = r,
                    r.User!.UserName,
                    r.User.DisplayName,
                    r.User.AvatarUrl,
                    Show = r.Show!,
                    SeasonId = r.SeasonNumber == null
                        ? (int?)null
                        : _context.Seasons
                            .Where(s => s.ShowId == r.ShowId && s.SeasonNumber == r.SeasonNumber)
                            .Select(s => (int?)s.Id)
                            .FirstOrDefault()
                })
                .ToListAsync();

            var userIds = rows.Select(x => x.Review.UserId).Distinct().ToList();
            var showIds = rows.Select(x => x.Review.ShowId).Distinct().ToList();
            var seasonIds = rows.Where(x => x.SeasonId != null).Select(x => x.SeasonId!.Value).Distinct().ToList();

            var ratings = await _context.Ratings
                .Where(r => userIds.Contains(r.UserId))
                .Where(r => (r.TargetType == RatingTargetType.Show && showIds.Contains(r.TargetId))
                         || (r.TargetType == RatingTargetType.Season && seasonIds.Contains(r.TargetId)))
                .ToListAsync();

            // Beğeni ve yorum sayıları tek sorguda toplanır; kart başına ek istek olmasın.
            var likeCounts = await _context.ReviewLikes
                .Where(l => reviewIds.Contains(l.ReviewId))
                .GroupBy(l => l.ReviewId)
                .Select(g => new { ReviewId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ReviewId, x => x.Count);

            var likedByViewer = viewerId == null
                ? new HashSet<int>()
                : (await _context.ReviewLikes
                    .Where(l => l.UserId == viewerId && reviewIds.Contains(l.ReviewId))
                    .Select(l => l.ReviewId)
                    .ToListAsync()).ToHashSet();

            var commentCounts = await _context.ReviewComments
                .Where(c => reviewIds.Contains(c.ReviewId))
                .GroupBy(c => c.ReviewId)
                .Select(g => new { ReviewId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ReviewId, x => x.Count);

            var byId = rows.ToDictionary(x => x.Review.Id, x =>
            {
                var r = x.Review;
                var targetType = r.SeasonNumber == null ? RatingTargetType.Show : RatingTargetType.Season;
                var targetId = r.SeasonNumber == null ? r.ShowId : x.SeasonId;

                return new ReviewDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    Username = x.UserName ?? string.Empty,
                    DisplayName = string.IsNullOrWhiteSpace(x.DisplayName) ? x.UserName ?? string.Empty : x.DisplayName,
                    AvatarUrl = x.AvatarUrl,
                    TmdbShowId = x.Show.TmdbId,
                    ShowName = x.Show.Name,
                    ShowPosterPath = x.Show.PosterPath,
                    SeasonNumber = r.SeasonNumber,
                    Body = r.Body,
                    HasSpoilers = r.HasSpoilers,
                    Rating = targetId == null
                        ? null
                        : ratings.FirstOrDefault(rt => rt.UserId == r.UserId
                                                    && rt.TargetType == targetType
                                                    && rt.TargetId == targetId)?.Value,
                    LikeCount = likeCounts.TryGetValue(r.Id, out var likes) ? likes : 0,
                    LikedByViewer = likedByViewer.Contains(r.Id),
                    CommentCount = commentCounts.TryGetValue(r.Id, out var comments) ? comments : 0,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                };
            });

            return reviewIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
        }
    }
}
