namespace BingeWatch.API.Models
{
    public enum NotificationType
    {
        /// <summary>Biri seni takip etmeye başladı.</summary>
        Followed = 1,

        /// <summary>İncelemeni beğendi.</summary>
        ReviewLiked = 2,

        /// <summary>İncelemene yorum yazdı.</summary>
        ReviewCommented = 3,

        /// <summary>Listeni beğendi.</summary>
        ListLiked = 4
    }

    /// <summary>
    /// Kullanıcıya gösterilen bildirim. Kendi eylemin sana bildirim üretmez;
    /// olayı geri alınca (takibi bırakma, beğeniyi kaldırma) bildirim de silinir.
    /// </summary>
    public class Notification
    {
        public int Id { get; set; }

        /// <summary>Bildirimi alan.</summary>
        public string UserId { get; set; } = string.Empty;
        public AppUser? User { get; set; }

        /// <summary>Eylemi yapan.</summary>
        public string ActorId { get; set; } = string.Empty;
        public AppUser? Actor { get; set; }

        public NotificationType Type { get; set; }

        /// <summary>Beğeni/yorum bildirimlerinde ilgili inceleme.</summary>
        public int? ReviewId { get; set; }

        /// <summary>Liste beğenisi bildirimlerinde ilgili liste.</summary>
        public int? UserListId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary><c>null</c> ise okunmamış.</summary>
        public DateTime? ReadAt { get; set; }
    }
}
