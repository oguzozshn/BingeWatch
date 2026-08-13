namespace BingeWatch.API.Dtos
{
    /// <summary>
    /// Bir kullanıcının kütüphanesi: listesindeki tüm diziler, durumlarıyla.
    /// Arayüz bunu iki sekmeye bölüyor (izledikleri / izleyecekleri); ayrı iki
    /// uç yerine tek yanıt, çünkü sekme değiştirmek yeni bir istek gerektirmemeli.
    /// </summary>
    public class UserLibraryDto
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        public List<LibraryShowDto> Shows { get; set; } = new();
    }

    /// <summary>
    /// Kütüphanedeki bir satır. <b>Bölüm bazlı ilerleme bilerek yok</b> — dizi
    /// seviyesindeki durum yeterli. "Şu bölümde" bilgisi kimseye lazım değil ve
    /// bölüm tartışmalarının kapısı o veriye dayanıyor: dışarıdan okunabilir
    /// olması, kimin hangi ipliği açabildiğini de okunabilir kılardı.
    /// </summary>
    public class LibraryShowDto
    {
        public int TmdbId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PosterPath { get; set; }

        /// <summary><c>WatchStatus</c> adı: PlanToWatch, Watching, Completed, Dropped, OnHold.</summary>
        public string Status { get; set; } = string.Empty;

        public bool IsFavorite { get; set; }
    }
}
