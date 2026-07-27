namespace BingeWatch.Web.Dtos
{
    public class UserStatsDto
    {
        public string Username { get; set; } = string.Empty;

        public int WatchedEpisodeCount { get; set; }
        public int ShowsWatchingCount { get; set; }
        public int ShowsCompletedCount { get; set; }
        public int ReviewCount { get; set; }
        public int RatingCount { get; set; }
        public double? AverageRating { get; set; }
        public int TotalMinutes { get; set; }

        public List<FavoriteShowDto> FavoriteShows { get; set; } = new();
        public List<YearlyCountDto> YearlyCounts { get; set; } = new();
    }

    public class FavoriteShowDto
    {
        public int TmdbId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
    }

    public class YearlyCountDto
    {
        public int Year { get; set; }
        public int EpisodeCount { get; set; }
    }

    public class SetFavoriteRequest
    {
        public bool IsFavorite { get; set; }
    }
}
