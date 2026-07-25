namespace BingeWatch.API.Dtos
{
    public class UpsertReviewRequest
    {
        /// <summary><c>null</c> ise dizi geneli inceleme; doluysa o sezona ait.</summary>
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

        /// <summary>Yazarın aynı hedefe verdiği puan (varsa) — inceleme kartında gösterilir.</summary>
        public decimal? Rating { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
