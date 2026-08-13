namespace BingeWatch.Web.Dtos
{
    public class UserLibraryDto
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<LibraryShowDto> Shows { get; set; } = new();
    }

    /// <summary>
    /// Bölüm bazlı ilerleme bilerek yok; API de göndermiyor. Dizi seviyesindeki
    /// durum yeterli (ROADMAP Faz 9.5).
    /// </summary>
    public class LibraryShowDto
    {
        public int TmdbId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsFavorite { get; set; }
    }
}
