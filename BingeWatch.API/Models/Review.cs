namespace BingeWatch.API.Models
{
    /// <summary>
    /// Yazılı inceleme. Bilinçli olarak yalnızca dizi ve sezon seviyesinde tutulur;
    /// bölüm bazlı yazılı inceleme aktivite akışını spoiler çöplüğüne çevirir
    /// (bkz. ROADMAP §3).
    /// </summary>
    public class Review
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public AppUser? User { get; set; }

        /// <summary>Yerel katalog id'si (TMDb id'si değil).</summary>
        public int ShowId { get; set; }
        public Show? Show { get; set; }

        /// <summary><c>null</c> ise dizi geneli; doluysa o sezonun incelemesi.</summary>
        public int? SeasonNumber { get; set; }

        public string Body { get; set; } = string.Empty;

        /// <summary>İşaretliyse gövde varsayılan olarak gizlenip "spoiler" uyarısıyla gösterilir.</summary>
        public bool HasSpoilers { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
