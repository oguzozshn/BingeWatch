using BingeWatch.API.Dtos;

namespace BingeWatch.API.Services
{
    public interface IReviewService
    {
        /// <summary>
        /// İncelemeyi yazar ya da aynı hedefte varsa günceller. Dizi TMDb'de yoksa
        /// veya gövde boşsa <c>null</c> döner.
        /// </summary>
        Task<ReviewDto?> UpsertAsync(string userId, int showTmdbId, UpsertReviewRequest request);

        /// <summary>İncelemeyi siler. Kayıt yoksa ya da sahibi değilse <c>false</c>.</summary>
        Task<bool> DeleteAsync(string userId, int reviewId);

        /// <summary>
        /// Bir dizinin incelemeleri, yeniden eskiye. <paramref name="viewerId"/> beğeni
        /// durumunu doldurmak için; anonimde <c>null</c> geçilir.
        /// </summary>
        Task<List<ReviewDto>> GetForShowAsync(int showTmdbId, int? seasonNumber = null, string? viewerId = null);

        /// <summary>Kullanıcının bu dizideki kendi incelemeleri (dizi geneli + sezonlar).</summary>
        Task<List<ReviewDto>> GetOwnForShowAsync(string userId, int showTmdbId);

        /// <summary>Genel inceleme akışı (<c>/reviews</c>).</summary>
        /// <summary>Genel inceleme akışı; imleç tabanlı sayfalama.</summary>
        Task<PagedResult<ReviewDto>> GetFeedAsync(string? cursor, int take, ReviewSort sort,
            string? viewerId = null);
    }

    public enum ReviewSort
    {
        Newest = 0,
        Oldest = 1,
        HighestRated = 2
    }
}
