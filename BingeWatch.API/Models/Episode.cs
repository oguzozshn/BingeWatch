namespace BingeWatch.API.Models
{
    public class Episode
    {
        public int Id { get; set; }

        public int SeasonId { get; set; }
        public Season? Season { get; set; }

        public int EpisodeNumber { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Overview { get; set; }
        public string? StillPath { get; set; }

        public DateTime? AirDate { get; set; }

        /// <summary>Dakika. TMDb her bölüm için vermeyebilir.</summary>
        public int? Runtime { get; set; }

        public double TmdbVoteAverage { get; set; }
        public int TmdbVoteCount { get; set; }
    }
}
