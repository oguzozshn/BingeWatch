namespace BingeWatch.Web.Dtos
{
    /// <summary>API'deki <c>NotificationType</c>'ın aynası.</summary>
    public enum NotificationType
    {
        Followed = 1,
        ReviewLiked = 2,
        ReviewCommented = 3
    }

    public class NotificationDto
    {
        public int Id { get; set; }
        public NotificationType Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }

        public string ActorUsername { get; set; } = string.Empty;
        public string ActorDisplayName { get; set; } = string.Empty;
        public string? ActorAvatarUrl { get; set; }

        public int? ReviewId { get; set; }
        public int? TmdbShowId { get; set; }
        public string? ShowName { get; set; }
        public int? SeasonNumber { get; set; }
    }
}
