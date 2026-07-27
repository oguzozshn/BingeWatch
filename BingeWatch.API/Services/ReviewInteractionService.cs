using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BingeWatch.API.Services
{
    public class ReviewInteractionService : IReviewInteractionService
    {
        private readonly BingeOnDbContext _context;
        private readonly INotificationService _notificationService;

        public ReviewInteractionService(BingeOnDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<ReviewLikeStateDto?> LikeAsync(string userId, int reviewId)
        {
            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId);
            if (review == null)
                return null;

            var existing = await _context.ReviewLikes
                .FirstOrDefaultAsync(l => l.ReviewId == reviewId && l.UserId == userId);

            if (existing == null)
            {
                _context.ReviewLikes.Add(new ReviewLike { ReviewId = reviewId, UserId = userId });
                await _context.SaveChangesAsync();

                await _notificationService.CreateAsync(review.UserId, userId,
                    NotificationType.ReviewLiked, reviewId);
            }

            return await BuildStateAsync(reviewId, userId);
        }

        public async Task<ReviewLikeStateDto?> UnlikeAsync(string userId, int reviewId)
        {
            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId);
            if (review == null)
                return null;

            var existing = await _context.ReviewLikes
                .FirstOrDefaultAsync(l => l.ReviewId == reviewId && l.UserId == userId);

            if (existing != null)
            {
                _context.ReviewLikes.Remove(existing);
                await _context.SaveChangesAsync();

                await _notificationService.RemoveAsync(review.UserId, userId,
                    NotificationType.ReviewLiked, reviewId);
            }

            return await BuildStateAsync(reviewId, userId);
        }

        public async Task<List<ReviewCommentDto>?> GetCommentsAsync(int reviewId, string? viewerId)
        {
            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId);
            if (review == null)
                return null;

            var comments = await _context.ReviewComments
                .Where(c => c.ReviewId == reviewId)
                .OrderBy(c => c.CreatedAt)
                .Include(c => c.User)
                .ToListAsync();

            return comments.Select(c => Project(c, review.UserId, viewerId)).ToList();
        }

        public async Task<ReviewCommentDto?> AddCommentAsync(string userId, int reviewId, AddCommentRequest request)
        {
            var body = request.Body?.Trim() ?? string.Empty;
            if (body.Length == 0)
                return null;

            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId);
            if (review == null)
                return null;

            var comment = new ReviewComment { ReviewId = reviewId, UserId = userId, Body = body };
            _context.ReviewComments.Add(comment);
            await _context.SaveChangesAsync();

            await _notificationService.CreateAsync(review.UserId, userId,
                NotificationType.ReviewCommented, reviewId);

            await _context.Entry(comment).Reference(c => c.User).LoadAsync();
            return Project(comment, review.UserId, userId);
        }

        public async Task<bool> DeleteCommentAsync(string userId, int commentId)
        {
            var comment = await _context.ReviewComments
                .Include(c => c.Review)
                .FirstOrDefaultAsync(c => c.Id == commentId);
            if (comment == null)
                return false;

            // Kendi yorumunu herkes siler; inceleme sahibi kendi incelemesindeki
            // yorumları da temizleyebilir (asgari moderasyon).
            if (comment.UserId != userId && comment.Review?.UserId != userId)
                return false;

            _context.ReviewComments.Remove(comment);
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<ReviewLikeStateDto> BuildStateAsync(int reviewId, string viewerId)
        {
            return new ReviewLikeStateDto
            {
                ReviewId = reviewId,
                LikeCount = await _context.ReviewLikes.CountAsync(l => l.ReviewId == reviewId),
                LikedByViewer = await _context.ReviewLikes
                    .AnyAsync(l => l.ReviewId == reviewId && l.UserId == viewerId)
            };
        }

        private static ReviewCommentDto Project(ReviewComment comment, string reviewOwnerId, string? viewerId) =>
            new()
            {
                Id = comment.Id,
                ReviewId = comment.ReviewId,
                Username = comment.User?.UserName ?? string.Empty,
                DisplayName = string.IsNullOrWhiteSpace(comment.User?.DisplayName)
                    ? comment.User?.UserName ?? string.Empty
                    : comment.User!.DisplayName,
                AvatarUrl = comment.User?.AvatarUrl,
                Body = comment.Body,
                CreatedAt = comment.CreatedAt,
                CanDelete = viewerId != null && (comment.UserId == viewerId || reviewOwnerId == viewerId)
            };
    }
}
