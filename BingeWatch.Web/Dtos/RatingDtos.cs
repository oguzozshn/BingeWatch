namespace BingeWatch.Web.Dtos
{
    /// <summary>API'deki <c>BingeWatch.API.Models.RatingTargetType</c>'ın aynası.</summary>
    public enum RatingTargetType
    {
        Show = 0,
        Season = 1,
        Episode = 2
    }

    public class SetRatingRequest
    {
        public RatingTargetType TargetType { get; set; }
        public int? SeasonNumber { get; set; }
        public int? EpisodeId { get; set; }
        public decimal Value { get; set; }
    }

    public class RatingDto
    {
        public RatingTargetType TargetType { get; set; }
        public int TargetId { get; set; }
        public int? SeasonNumber { get; set; }
        public int? EpisodeId { get; set; }
        public decimal Value { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ShowRatingsDto
    {
        public int TmdbId { get; set; }
        public decimal? ShowRating { get; set; }
        public Dictionary<int, decimal> SeasonRatings { get; set; } = new();
        public Dictionary<int, decimal> EpisodeRatings { get; set; } = new();
    }

    public class RatingSummaryDto
    {
        public int TmdbId { get; set; }
        public double? Average { get; set; }
        public int Count { get; set; }
        public Dictionary<string, int> Distribution { get; set; } = new();
    }

    /// <summary>Dizi sayfasındaki "takip ettiklerinin puanı" kartı.</summary>
    public class FriendRatingsDto
    {
        public int TmdbId { get; set; }
        public double? Average { get; set; }
        public int Count { get; set; }
        public List<FriendRatingDto> Ratings { get; set; } = new();
    }

    public class FriendRatingDto
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public decimal Value { get; set; }
    }
}
