using Microsoft.EntityFrameworkCore;
using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;

namespace BingeWatch.API.Services
{
    public class RatingService : IRatingService
    {
        private readonly BingeOnDbContext _context;
        private readonly IActivityService _activityService;

        public RatingService(BingeOnDbContext context, IActivityService activityService)
        {
            _context = context;
            _activityService = activityService;
        }

        public async Task<RatingDto?> SetRatingAsync(string userId, int showTmdbId, SetRatingRequest request)
        {
            if (!Rating.IsValidValue(request.Value))
                return null;

            var resolved = await ResolveTargetAsync(showTmdbId, request);
            if (resolved == null)
                return null;

            var (showId, target) = resolved.Value;

            var existing = await _context.Ratings.FirstOrDefaultAsync(r =>
                r.UserId == userId && r.TargetType == request.TargetType && r.TargetId == target);

            if (existing == null)
            {
                existing = new Rating
                {
                    UserId = userId,
                    TargetType = request.TargetType,
                    TargetId = target,
                    Value = request.Value
                };
                _context.Ratings.Add(existing);
            }
            else
            {
                existing.Value = request.Value;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            await _activityService.RecordRatedAsync(userId, showId, request.TargetType,
                request.SeasonNumber, request.EpisodeId, request.Value);

            return new RatingDto
            {
                TargetType = existing.TargetType,
                TargetId = existing.TargetId,
                SeasonNumber = request.SeasonNumber,
                EpisodeId = request.EpisodeId,
                Value = existing.Value,
                UpdatedAt = existing.UpdatedAt
            };
        }

        public async Task<bool> RemoveRatingAsync(string userId, int showTmdbId, SetRatingRequest request)
        {
            var resolved = await ResolveTargetAsync(showTmdbId, request);
            if (resolved == null)
                return false;

            var (showId, target) = resolved.Value;

            var existing = await _context.Ratings.FirstOrDefaultAsync(r =>
                r.UserId == userId && r.TargetType == request.TargetType && r.TargetId == target);
            if (existing == null)
                return false;

            _context.Ratings.Remove(existing);
            await _context.SaveChangesAsync();

            await _activityService.RemoveRatedAsync(userId, showId, request.TargetType,
                request.SeasonNumber, request.EpisodeId);

            return true;
        }

        public async Task<ShowRatingsDto?> GetUserRatingsForShowAsync(string userId, int showTmdbId)
        {
            var show = await _context.Shows
                .Include(s => s.Seasons)
                .FirstOrDefaultAsync(s => s.TmdbId == showTmdbId);
            if (show == null)
                return null;

            var seasonIdToNumber = show.Seasons.ToDictionary(s => s.Id, s => s.SeasonNumber);
            var seasonIds = seasonIdToNumber.Keys.ToList();

            var episodeIds = await _context.Episodes
                .Where(e => seasonIds.Contains(e.SeasonId))
                .Select(e => e.Id)
                .ToListAsync();

            var ratings = await _context.Ratings
                .Where(r => r.UserId == userId)
                .Where(r => (r.TargetType == RatingTargetType.Show && r.TargetId == show.Id)
                         || (r.TargetType == RatingTargetType.Season && seasonIds.Contains(r.TargetId))
                         || (r.TargetType == RatingTargetType.Episode && episodeIds.Contains(r.TargetId)))
                .ToListAsync();

            return new ShowRatingsDto
            {
                TmdbId = show.TmdbId,
                ShowRating = ratings.FirstOrDefault(r => r.TargetType == RatingTargetType.Show)?.Value,
                SeasonRatings = ratings
                    .Where(r => r.TargetType == RatingTargetType.Season)
                    .ToDictionary(r => seasonIdToNumber[r.TargetId], r => r.Value),
                EpisodeRatings = ratings
                    .Where(r => r.TargetType == RatingTargetType.Episode)
                    .ToDictionary(r => r.TargetId, r => r.Value)
            };
        }

        public async Task<RatingSummaryDto?> GetShowSummaryAsync(int showTmdbId)
        {
            var show = await _context.Shows.FirstOrDefaultAsync(s => s.TmdbId == showTmdbId);
            if (show == null)
                return null;

            var values = await _context.Ratings
                .Where(r => r.TargetType == RatingTargetType.Show && r.TargetId == show.Id)
                .Select(r => r.Value)
                .ToListAsync();

            // Kovalar puan verilmemiş olsa bile 0 ile dolu gelsin ki histogram boşluksuz çizilsin.
            var distribution = new Dictionary<string, int>();
            for (var step = 1; step <= 10; step++)
            {
                var bucket = step * 0.5m;
                distribution[bucket.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)] =
                    values.Count(v => v == bucket);
            }

            return new RatingSummaryDto
            {
                TmdbId = show.TmdbId,
                Average = values.Count == 0 ? null : (double)values.Average(),
                Count = values.Count,
                Distribution = distribution
            };
        }

        /// <summary>
        /// İsteğin hedefini yerel katalog id'sine çevirir; dizinin kendi id'sini de döner
        /// (aktivite olayı dizi bazında yazılıyor). Hedef bulunamazsa <c>null</c>; böylece
        /// başka bir dizinin bölümüne bu dizi üzerinden puan verilemez.
        /// </summary>
        private async Task<(int ShowId, int TargetId)?> ResolveTargetAsync(int showTmdbId, SetRatingRequest request)
        {
            var show = await _context.Shows.FirstOrDefaultAsync(s => s.TmdbId == showTmdbId);
            if (show == null)
                return null;

            switch (request.TargetType)
            {
                case RatingTargetType.Show:
                    return (show.Id, show.Id);

                case RatingTargetType.Season:
                    if (request.SeasonNumber == null)
                        return null;
                    var season = await _context.Seasons.FirstOrDefaultAsync(s =>
                        s.ShowId == show.Id && s.SeasonNumber == request.SeasonNumber);
                    return season == null ? null : (show.Id, season.Id);

                case RatingTargetType.Episode:
                    if (request.EpisodeId == null)
                        return null;
                    var episode = await _context.Episodes
                        .Include(e => e.Season)
                        .FirstOrDefaultAsync(e => e.Id == request.EpisodeId && e.Season!.ShowId == show.Id);
                    return episode == null ? null : (show.Id, episode.Id);

                default:
                    return null;
            }
        }
    }
}
