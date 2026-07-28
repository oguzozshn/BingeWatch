using BingeWatch.API.Dtos;

namespace BingeWatch.API.Services
{
    public interface IEpisodeCommentService
    {
        /// <summary>
        /// Bölümün yorum ipliği. Bölüm yoksa <c>null</c>; isteği yapan bölümü
        /// izlememişse kilitli ve boş iplik döner.
        /// </summary>
        Task<EpisodeCommentThreadDto?> GetThreadAsync(int episodeId, string? viewerId);

        /// <summary>
        /// Yorum ekler. Bölüm yoksa, gövde boşsa ya da kullanıcı bölümü
        /// izlememişse <c>null</c> döner.
        /// </summary>
        Task<EpisodeCommentDto?> AddAsync(string userId, int episodeId, AddEpisodeCommentRequest request);

        /// <summary>Yorumu siler; yalnızca yorumun sahibi silebilir.</summary>
        Task<bool> DeleteAsync(string userId, int commentId);
    }
}
