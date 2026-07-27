namespace BingeWatch.Web.Dtos
{
    public class UpsertReviewRequest
    {
        public int? SeasonNumber { get; set; }
        public string Body { get; set; } = string.Empty;
        public bool HasSpoilers { get; set; }
    }

    public class ReviewDto
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }

        public int TmdbShowId { get; set; }
        public string ShowName { get; set; } = string.Empty;
        public string? ShowPosterPath { get; set; }

        public int? SeasonNumber { get; set; }
        public string Body { get; set; } = string.Empty;
        public bool HasSpoilers { get; set; }
        public decimal? Rating { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>API'deki <c>ReviewSort</c>'un aynası — sorgu dizesinde sayı olarak gider.</summary>
    public enum ReviewSort
    {
        Newest = 0,
        Oldest = 1,
        HighestRated = 2
    }
}
