namespace BingeWatch.API.Models
{
    /// <summary>
    /// Kullanıcının bir diziye / sezona / bölüme verdiği yarım yıldızlı puan (0.5–5.0).
    /// <see cref="TargetId"/> hedef tipe göre <c>Show.Id</c>, <c>Season.Id</c> ya da
    /// <c>Episode.Id</c>'dir — TMDb id'si değil, yerel katalog id'si.
    /// </summary>
    public class Rating
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public AppUser? User { get; set; }

        public RatingTargetType TargetType { get; set; }
        public int TargetId { get; set; }

        /// <summary>0.5 ile 5.0 arası, 0.5 adımlı.</summary>
        public decimal Value { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Geçerli puan aralığı ve adımı — servis ve testler bunu paylaşır.</summary>
        public static bool IsValidValue(decimal value) =>
            value >= 0.5m && value <= 5.0m && value % 0.5m == 0m;
    }
}
