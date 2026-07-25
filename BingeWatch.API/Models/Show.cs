namespace BingeWatch.API.Models
{
    /// <summary>
    /// TMDb dizisinin yerel kopyası. Bölüm bazlı takip ve toplu istatistik her istekte
    /// TMDb'ye gidilerek yapılamaz; katalog burada tutulup periyodik senkronize edilir.
    /// </summary>
    public class Show
    {
        public int Id { get; set; }

        /// <summary>TMDb dizi kimliği — dış dünyada tekil anahtar.</summary>
        public int TmdbId { get; set; }

        public string? ImdbId { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Overview { get; set; } = string.Empty;

        /// <summary>Her zaman TMDb'ye göreli yol ("/abc.jpg"), tam URL değil.</summary>
        public string? PosterPath { get; set; }
        public string? BackdropPath { get; set; }

        public DateTime? FirstAirDate { get; set; }

        /// <summary>TMDb durumu: "Returning Series", "Ended", "Canceled"...</summary>
        public string? TmdbStatus { get; set; }

        public double VoteAverage { get; set; }
        public int VoteCount { get; set; }

        /// <summary>
        /// Katalogun TMDb ile en son ne zaman eşitlendiği. <c>default</c> ise satır
        /// yalnızca bir taslaktır (ör. eski watchlist verisinden üretilmiş) ve ilk
        /// erişimde doldurulmalıdır.
        /// </summary>
        public DateTime LastSyncedAt { get; set; }

        public List<Season> Seasons { get; set; } = new();
    }
}
