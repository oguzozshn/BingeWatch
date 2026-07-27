namespace BingeWatch.Web.Dtos
{
    public enum ActivityType
    {
        Rated = 1,
        Reviewed = 2,
        Watched = 3,
        Followed = 4
    }

    public class ActivityDto
    {
        public int Id { get; set; }
        public ActivityType Type { get; set; }
        public DateTime CreatedAt { get; set; }

        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }

        public int? TmdbShowId { get; set; }
        public string? ShowName { get; set; }
        public string? ShowPosterPath { get; set; }
        public int? SeasonNumber { get; set; }
        public int? EpisodeNumber { get; set; }
        public string? EpisodeName { get; set; }
        public int? EpisodeCount { get; set; }

        public decimal? RatingValue { get; set; }

        public int? ReviewId { get; set; }
        public string? ReviewExcerpt { get; set; }
        public bool ReviewHasSpoilers { get; set; }

        public string? TargetUsername { get; set; }
        public string? TargetDisplayName { get; set; }
    }
}
