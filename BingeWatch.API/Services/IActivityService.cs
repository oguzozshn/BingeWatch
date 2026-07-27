using BingeWatch.API.Dtos;
using BingeWatch.API.Models;

namespace BingeWatch.API.Services
{
    /// <summary>
    /// Aktivite akışının yazma ve okuma tarafı. Yazma çağrıları kaynak servislerden
    /// (puan, inceleme, izleme, takip) yapılır; akış okuması fan-out yapar.
    /// </summary>
    public interface IActivityService
    {
        /// <summary>Puan olayını yazar; aynı hedefte olay varsa değeri güncelleyip tarihi tazeler.</summary>
        Task RecordRatedAsync(string userId, int showId, RatingTargetType targetType,
            int? seasonNumber, int? episodeId, decimal value);

        Task RemoveRatedAsync(string userId, int showId, RatingTargetType targetType,
            int? seasonNumber, int? episodeId);

        /// <summary>İnceleme olayını yazar; aynı hedefte olay varsa tarihini tazeler.</summary>
        Task RecordReviewedAsync(string userId, int reviewId, int showId, int? seasonNumber);

        Task RemoveReviewedAsync(int reviewId);

        /// <summary>Tek olay yazar: <paramref name="episodeCount"/> bölüm, sonuncusu <paramref name="lastEpisodeId"/>.</summary>
        Task RecordWatchedAsync(string userId, int showId, int lastEpisodeId, int episodeCount);

        /// <summary>İşareti kaldırılan bölümlere ait izleme olaylarını siler.</summary>
        Task RemoveWatchedAsync(string userId, IReadOnlyCollection<int> episodeIds);

        Task RecordFollowedAsync(string followerId, string followeeId);

        Task RemoveFollowedAsync(string followerId, string followeeId);

        /// <summary>Takip edilenlerin ve kullanıcının kendi olayları, en yeniden eskiye.</summary>
        Task<List<ActivityDto>> GetFeedAsync(string viewerId, int skip, int take);
    }
}
