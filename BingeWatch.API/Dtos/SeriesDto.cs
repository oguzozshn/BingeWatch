namespace BingeWatch.API.Dtos
{
    public class SeriesDto
    {
        /// <summary>TMDb dizi kimliği.</summary>
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Overview { get; set; } = string.Empty;

        /// <summary>TMDb'ye göreli yol ("/abc.jpg"); tam URL değil.</summary>
        public string PosterPath { get; set; } = string.Empty;
        public DateTime? FirstAirDate { get; set; }

        /// <summary>
        /// Katalogdan geliyorsa dolu olur. Web'in her poster için ayrı bir
        /// external_ids isteği atmasını (N+1) gereksiz kılar.
        /// </summary>
        public string? ImdbId { get; set; }

        /// <summary>TMDb kullanıcı puanı (0–10).</summary>
        public double VoteAverage { get; set; }

        /// <summary>Kaç oydan geldiği — tek oylu diziler puanı anlamsız kılıyor.</summary>
        public int VoteCount { get; set; }
    }
}
