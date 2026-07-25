namespace BingeWatch.Web.Dtos
{
    public class SeriesDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Overview { get; set; } = string.Empty;
        public string PosterPath { get; set; } = string.Empty;
        public string? FirstAirDate { get; set; }

        /// <summary>Katalogdan geliyorsa dolu olur; TMDb'ye ayrıca external_ids isteği atılmaz.</summary>
        public string? ImdbId { get; set; }
    }
}
