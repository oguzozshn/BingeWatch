namespace BingeWatch.Web.Dtos
{
    public class UserStatsDto
    {
        public string Username { get; set; } = string.Empty;

        public int WatchedEpisodeCount { get; set; }
        public int RewatchCount { get; set; }
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

    public class UserStatsDetailDto
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        public int WatchedEpisodeCount { get; set; }
        public int RewatchCount { get; set; }
        public int TotalMinutes { get; set; }
        public int EpisodesWithoutRuntime { get; set; }

        public int ShowCount { get; set; }
        public int ShowsWatchingCount { get; set; }
        public int ShowsCompletedCount { get; set; }
        public int ShowsDroppedCount { get; set; }
        public int ShowsPlannedCount { get; set; }
        public int ShowsOnHoldCount { get; set; }

        public int ReviewCount { get; set; }
        public int RatingCount { get; set; }
        public double? AverageRating { get; set; }

        public List<YearlyStatDto> Yearly { get; set; } = new();
        public List<GenreStatDto> Genres { get; set; } = new();
        public List<TopShowDto> TopShows { get; set; } = new();
        public List<RatingBucketDto> RatingDistribution { get; set; } = new();
    }

    public class YearlyStatDto
    {
        public int Year { get; set; }
        public int EpisodeCount { get; set; }
        public int Minutes { get; set; }
    }

    public class GenreStatDto
    {
        public int GenreId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int EpisodeCount { get; set; }
        public int ShowCount { get; set; }
    }

    public class TopShowDto
    {
        public int TmdbId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
        public int EpisodeCount { get; set; }
        public int Minutes { get; set; }
    }

    public class RatingBucketDto
    {
        public decimal Value { get; set; }
        public int Count { get; set; }
    }
}
