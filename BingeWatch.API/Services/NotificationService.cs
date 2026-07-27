using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BingeWatch.API.Services
{
    public class NotificationService : INotificationService
    {
        private readonly BingeOnDbContext _context;

        public NotificationService(BingeOnDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(string recipientId, string actorId, NotificationType type, int? reviewId = null)
        {
            // Kendi incelemeni beğenmek ya da kendini takip etmeye çalışmak bildirim üretmez.
            if (recipientId == actorId)
                return;

            var exists = await _context.Notifications.AnyAsync(n =>
                n.UserId == recipientId && n.ActorId == actorId && n.Type == type && n.ReviewId == reviewId);
            if (exists)
                return;

            _context.Notifications.Add(new Notification
            {
                UserId = recipientId,
                ActorId = actorId,
                Type = type,
                ReviewId = reviewId
            });

            await _context.SaveChangesAsync();
        }

        public async Task RemoveAsync(string recipientId, string actorId, NotificationType type, int? reviewId = null)
        {
            var rows = await _context.Notifications
                .Where(n => n.UserId == recipientId && n.ActorId == actorId
                            && n.Type == type && n.ReviewId == reviewId)
                .ToListAsync();
            if (rows.Count == 0)
                return;

            _context.Notifications.RemoveRange(rows);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveForReviewAsync(int reviewId)
        {
            var rows = await _context.Notifications
                .Where(n => n.ReviewId == reviewId)
                .ToListAsync();
            if (rows.Count == 0)
                return;

            _context.Notifications.RemoveRange(rows);
            await _context.SaveChangesAsync();
        }

        public async Task<List<NotificationDto>> GetAsync(string userId, int skip, int take)
        {
            take = Math.Clamp(take, 1, 100);
            skip = Math.Max(skip, 0);

            var rows = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ThenByDescending(n => n.Id)
                .Skip(skip)
                .Take(take)
                .Include(n => n.Actor)
                .ToListAsync();

            var reviewIds = rows.Where(n => n.ReviewId != null).Select(n => n.ReviewId!.Value).Distinct().ToList();
            var reviews = reviewIds.Count == 0
                ? new Dictionary<int, (int TmdbId, string Name, int? SeasonNumber)>()
                : await _context.Reviews
                    .Where(r => reviewIds.Contains(r.Id))
                    .Select(r => new { r.Id, r.Show!.TmdbId, r.Show.Name, r.SeasonNumber })
                    .ToDictionaryAsync(r => r.Id, r => (r.TmdbId, r.Name, r.SeasonNumber));

            return rows.Select(n =>
            {
                (int TmdbId, string Name, int? SeasonNumber) review = default;
                if (n.ReviewId != null)
                    reviews.TryGetValue(n.ReviewId.Value, out review);

                return new NotificationDto
                {
                    Id = n.Id,
                    Type = n.Type,
                    CreatedAt = n.CreatedAt,
                    IsRead = n.ReadAt != null,
                    ActorUsername = n.Actor?.UserName ?? string.Empty,
                    ActorDisplayName = string.IsNullOrWhiteSpace(n.Actor?.DisplayName)
                        ? n.Actor?.UserName ?? string.Empty
                        : n.Actor!.DisplayName,
                    ActorAvatarUrl = n.Actor?.AvatarUrl,
                    ReviewId = n.ReviewId,
                    TmdbShowId = review.TmdbId == 0 ? null : review.TmdbId,
                    ShowName = review.Name,
                    SeasonNumber = review.SeasonNumber
                };
            }).ToList();
        }

        public Task<int> GetUnreadCountAsync(string userId) =>
            _context.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null);

        public async Task<int> MarkAllReadAsync(string userId)
        {
            var unread = await _context.Notifications
                .Where(n => n.UserId == userId && n.ReadAt == null)
                .ToListAsync();
            if (unread.Count == 0)
                return 0;

            var now = DateTime.UtcNow;
            foreach (var notification in unread)
                notification.ReadAt = now;

            await _context.SaveChangesAsync();
            return unread.Count;
        }
    }
}
