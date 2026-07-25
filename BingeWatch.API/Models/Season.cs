namespace BingeWatch.API.Models
{
    public class Season
    {
        public int Id { get; set; }

        public int ShowId { get; set; }
        public Show? Show { get; set; }

        /// <summary>TMDb sezon numarası. 0 = özel bölümler ("Specials").</summary>
        public int SeasonNumber { get; set; }

        public string? Name { get; set; }
        public string? Overview { get; set; }
        public string? PosterPath { get; set; }
        public DateTime? AirDate { get; set; }

        /// <summary>TMDb'nin bildirdiği bölüm sayısı; yerelde çekilen sayıdan farklı olabilir.</summary>
        public int EpisodeCount { get; set; }

        public List<Episode> Episodes { get; set; } = new();
    }
}
