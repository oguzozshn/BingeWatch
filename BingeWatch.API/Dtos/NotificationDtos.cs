using BingeWatch.API.Models;

namespace BingeWatch.API.Dtos
{
    public class NotificationDto
    {
        public int Id { get; set; }
        public NotificationType Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }

        // Eylemi yapan
        public string ActorUsername { get; set; } = string.Empty;
        public string ActorDisplayName { get; set; } = string.Empty;
        public string? ActorAvatarUrl { get; set; }

        // Beğeni/yorum bildirimlerinde incelemenin dizisi — karttan diziye gidilebilsin
        public int? ReviewId { get; set; }
        public int? TmdbShowId { get; set; }
        public string? ShowName { get; set; }
        public int? SeasonNumber { get; set; }
    }
}
