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

        public ReviewService(BingeOnDbContext context, IShowCatalogService catalogService,
            IActivityService activityService)
        {
            _context = context;
            _catalogService = catalogService;
            _activityService = activityService;
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

            return true;
        }

        public async Task<List<ReviewDto>> GetForShowAsync(int showTmdbId, int? seasonNumber = null)
        {
            var show = await _context.Shows.FirstOrDefaultAsync(s => s.TmdbId == showTmdbId);
            if (show == null)
                return new List<ReviewDto>();

            var query = _context.Reviews.Where(r => r.ShowId == show.Id);
            if (seasonNumber != null)
                query = query.Where(r => r.SeasonNumber == seasonNumber);

            var ids = await query
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => r.Id)
                .ToListAsync();

            return await ProjectAsync(ids);
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

            return await ProjectAsync(ids);
        }

        public async Task<List<ReviewDto>> GetFeedAsync(int skip, int take, ReviewSort sort)
        {
            take = Math.Clamp(take, 1, 100);
            skip = Math.Max(skip, 0);

            var ordered = sort switch
            {
                ReviewSort.Oldest => _context.Reviews.OrderBy(r => r.CreatedAt),
                _ => _context.Reviews.OrderByDescending(r => r.CreatedAt)
            };

            // HighestRated puana göre sıralanır; puan ayrı tabloda olduğu için
            // projeksiyondan sonra sıralamak, kova kova sayfalamaktan basit ve yeterli.
            var ids = await ordered.Skip(skip).Take(take).Select(r => r.Id).ToListAsync();
            var result = await ProjectAsync(ids);

            if (sort == ReviewSort.HighestRated)
                result = result.OrderByDescending(r => r.Rating ?? -1).ToList();

            return result;
        }

        /// <summary>
        /// İnceleme id'lerini yazarı, dizisi ve yazarın aynı hedefe verdiği puanla
        /// birlikte DTO'ya çevirir. Sıra <paramref name="reviewIds"/>'in sırasıdır.
        /// </summary>
        private async Task<List<ReviewDto>> ProjectAsync(IReadOnlyList<int> reviewIds)
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
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                };
            });

            return reviewIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
        }
    }
}
