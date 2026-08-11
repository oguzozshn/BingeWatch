using BingeWatch.API.Models;

namespace BingeWatch.API.Dtos
{
    /// <summary>
    /// Puan yazma isteği. Hedef her zaman <b>TMDb dizi id'si + seviye</b> ile adreslenir;
    /// istemcinin yerel katalog id'lerini bilmesi gerekmez.
    /// </summary>
    public class SetRatingRequest
    {
        public RatingTargetType TargetType { get; set; }

        /// <summary><see cref="RatingTargetType.Season"/> için gerekli.</summary>
        public int? SeasonNumber { get; set; }

        /// <summary><see cref="RatingTargetType.Episode"/> için gerekli — yerel bölüm id'si.</summary>
        public int? EpisodeId { get; set; }

        /// <summary>0.5–5.0 arası, 0.5 adımlı.</summary>
        public decimal Value { get; set; }
    }

    public class RatingDto
    {
        public RatingTargetType TargetType { get; set; }
        public int TargetId { get; set; }
        public int? SeasonNumber { get; set; }
        public int? EpisodeId { get; set; }
        public decimal Value { get; set; }
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Bu puan bölümü aynı istekte "izledim" olarak işaretlediyse <c>true</c>.
        /// İstemci izleme kutusunu ve ilerlemeyi tazelemek için buna bakar.
        /// </summary>
        public bool MarkedWatched { get; set; }
    }

    /// <summary>Bir dizinin kullanıcıya ait tüm puanları — dizi sayfası tek istekte alır.</summary>
    public class ShowRatingsDto
    {
        public int TmdbId { get; set; }
        public decimal? ShowRating { get; set; }

        /// <summary>Sezon numarası → puan.</summary>
        public Dictionary<int, decimal> SeasonRatings { get; set; } = new();

        /// <summary>Yerel bölüm id'si → puan.</summary>
        public Dictionary<int, decimal> EpisodeRatings { get; set; } = new();
    }

    /// <summary>Bir dizinin BingeWatch kullanıcı puanlarının özeti.</summary>
    public class RatingSummaryDto
    {
        public int TmdbId { get; set; }
        public double? Average { get; set; }
        public int Count { get; set; }

        /// <summary>0.5'ten 5.0'a 10 kova; anahtar puan değeri, değer kullanıcı sayısı.</summary>
        public Dictionary<string, int> Distribution { get; set; } = new();
    }

    /// <summary>Dizi sayfasındaki "takip ettiklerinin puanı" kartı.</summary>
    public class FriendRatingsDto
    {
        public int TmdbId { get; set; }
        public double? Average { get; set; }
        public int Count { get; set; }

        /// <summary>Puan veren takip edilenler, en yüksekten düşüğe.</summary>
        public List<FriendRatingDto> Ratings { get; set; } = new();
    }

    public class FriendRatingDto
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public decimal Value { get; set; }
    }
}
