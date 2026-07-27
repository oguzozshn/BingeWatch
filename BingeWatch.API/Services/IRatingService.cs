using BingeWatch.API.Dtos;

namespace BingeWatch.API.Services
{
    public interface IRatingService
    {
        /// <summary>
        /// Puanı yazar ya da günceller. Hedef katalogda yoksa (veya değer geçersizse)
        /// <c>null</c> döner.
        /// </summary>
        Task<RatingDto?> SetRatingAsync(string userId, int showTmdbId, SetRatingRequest request);

        /// <summary>Puanı siler. Silinecek satır yoksa <c>false</c>.</summary>
        Task<bool> RemoveRatingAsync(string userId, int showTmdbId, SetRatingRequest request);

        /// <summary>Kullanıcının bu diziye ait tüm puanları (dizi + sezonlar + bölümler).</summary>
        Task<ShowRatingsDto?> GetUserRatingsForShowAsync(string userId, int showTmdbId);

        /// <summary>Dizinin tüm kullanıcılar üzerinden ortalaması ve dağılım histogramı.</summary>
        Task<RatingSummaryDto?> GetShowSummaryAsync(int showTmdbId);

        /// <summary>
        /// Kullanıcının takip ettiklerinin bu diziye verdiği puanlar. Yalnızca dizi
        /// seviyesindeki puanlar sayılır; sezon/bölüm puanları karta girmez.
        /// </summary>
        Task<FriendRatingsDto?> GetFriendRatingsAsync(string userId, int showTmdbId);
    }
}
