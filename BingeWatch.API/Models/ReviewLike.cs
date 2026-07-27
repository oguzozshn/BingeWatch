namespace BingeWatch.API.Models
{
    /// <summary>İncelemeye verilen beğeni. Satırın varlığı beğeni demek; ayrıca bayrak tutulmaz.</summary>
    public class ReviewLike
    {
        public int Id { get; set; }

        public int ReviewId { get; set; }
        public Review? Review { get; set; }

        public string UserId { get; set; } = string.Empty;
        public AppUser? User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
