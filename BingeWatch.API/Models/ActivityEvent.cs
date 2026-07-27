namespace BingeWatch.API.Models
{
    public enum ActivityType
    {
        /// <summary>Puan verdi — seviye <see cref="ActivityEvent.SeasonNumber"/> / <see cref="ActivityEvent.EpisodeId"/> ile belli olur.</summary>
        Rated = 1,

        /// <summary>İnceleme yazdı (dizi ya da sezon).</summary>
        Reviewed = 2,

        /// <summary>Bölüm izledi. Toplu işaretlemede tek olay yazılır, <see cref="ActivityEvent.EpisodeCount"/> kaç bölüm olduğunu söyler.</summary>
        Watched = 3,

        /// <summary>Başka bir kullanıcıyı takip etmeye başladı.</summary>
        Followed = 4
    }

    /// <summary>
    /// Akış için denormalize edilmiş aktivite kaydı. Okuma tarafı fan-out yapar
    /// (takip edilenlerin olayları okunur), yazma tarafı tek satır ekler.
    ///
    /// Kaynak kayıt geri alınınca (puan silme, inceleme silme, takibi bırakma,
    /// bölüm işaretini kaldırma) ilgili olay da silinir; akışta hayalet kalmaz.
    /// </summary>
    public class ActivityEvent
    {
        public int Id { get; set; }

        /// <summary>Olayı üreten kullanıcı.</summary>
        public string UserId { get; set; } = string.Empty;

        public ActivityType Type { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Yerel katalog dizi id'si; <see cref="ActivityType.Followed"/> dışında dolu.</summary>
        public int? ShowId { get; set; }

        public int? SeasonNumber { get; set; }

        /// <summary>Bölüm hedefli olaylarda son bölüm.</summary>
        public int? EpisodeId { get; set; }

        /// <summary>Toplu izlemede işaretlenen bölüm sayısı; tekil izlemede 1.</summary>
        public int? EpisodeCount { get; set; }

        public decimal? RatingValue { get; set; }

        public int? ReviewId { get; set; }

        /// <summary>Takip olayında takip edilen kullanıcı.</summary>
        public string? TargetUserId { get; set; }

        public AppUser? User { get; set; }
        public Show? Show { get; set; }
        public Episode? Episode { get; set; }
        public AppUser? TargetUser { get; set; }
    }
}
