namespace BingeWatch.API.Models
{
    /// <summary>
    /// TMDb türü ("Drama", "Sci-Fi &amp; Fantasy"). TMDb'nin id'si birincil anahtar
    /// olarak kullanılır: liste sabit ve küçük, ayrı bir yerel id tutmanın faydası yok.
    /// </summary>
    public class Genre
    {
        /// <summary>TMDb tür kimliği.</summary>
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public List<Show> Shows { get; set; } = new();
    }
}
