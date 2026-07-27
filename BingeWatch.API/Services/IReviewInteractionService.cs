using BingeWatch.API.Dtos;

namespace BingeWatch.API.Services
{
    /// <summary>İnceleme beğenileri ve yorumları. Her ikisi de yazarına bildirim üretir.</summary>
    public interface IReviewInteractionService
    {
        /// <summary>Beğenir; zaten beğeniliyse durumu değiştirmez. İnceleme yoksa <c>null</c>.</summary>
        Task<ReviewLikeStateDto?> LikeAsync(string userId, int reviewId);

        Task<ReviewLikeStateDto?> UnlikeAsync(string userId, int reviewId);

        Task<List<ReviewCommentDto>?> GetCommentsAsync(int reviewId, string? viewerId);

        /// <summary>Yorum ekler. Gövde boşsa ya da inceleme yoksa <c>null</c>.</summary>
        Task<ReviewCommentDto?> AddCommentAsync(string userId, int reviewId, AddCommentRequest request);

        /// <summary>Yorumu siler. Yalnızca yorumun sahibi ya da incelemenin sahibi silebilir.</summary>
        Task<bool> DeleteCommentAsync(string userId, int commentId);
    }
}
