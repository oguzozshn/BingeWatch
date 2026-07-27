namespace BingeWatch.API.Models
{
    /// <summary>
    /// Listedeki tek bir dizi. Sıra kullanıcının verdiği anlamı taşıdığı için
    /// (1. sıra = en iyisi) <see cref="Position"/> kalıcı tutulur, ekleme tarihine
    /// göre sıralanmaz.
    /// </summary>
    public class UserListItem
    {
        public int Id { get; set; }

        public int UserListId { get; set; }
        public UserList? UserList { get; set; }

        /// <summary>Yerel katalog id'si (TMDb id'si değil).</summary>
        public int ShowId { get; set; }
        public Show? Show { get; set; }

        /// <summary>0'dan başlayan sıra. Liste içinde tekil olması servis tarafından korunur.</summary>
        public int Position { get; set; }

        /// <summary>Küratörün o diziye düştüğü not; isteğe bağlı.</summary>
        public string? Note { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}
