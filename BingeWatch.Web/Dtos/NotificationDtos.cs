namespace BingeWatch.Web.Dtos
{
    /// <summary>API'deki <c>NotificationType</c>'ın aynası.</summary>
    public enum NotificationType
    {
        Followed = 1,
        ReviewLiked = 2,
        ReviewCommented = 3,
        ListLiked = 4
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

        public int? UserListId { get; set; }
        public string? ListTitle { get; set; }
    }

    /// <summary>
    /// Bildirim metinleri. Zil paneli ve bildirimler sayfası aynı cümleleri
    /// kurduğu için tek yerde tutuluyor; ikisi ayrı yazılırsa biri güncellenip
    /// diğeri unutuluyor.
    /// </summary>
    public static class NotificationText
    {
        /// <summary>Aktörün adından sonra gelen cümle.</summary>
        public static string Sentence(NotificationDto notification) => notification.Type switch
        {
            NotificationType.Followed => "seni takip etmeye başladı",
            NotificationType.ReviewLiked => notification.SeasonNumber.HasValue
                ? $"{notification.SeasonNumber}. sezon incelemeni beğendi —"
                : "incelemeni beğendi —",
            NotificationType.ListLiked => "listeni beğendi —",
            _ => notification.SeasonNumber.HasValue
                ? $"{notification.SeasonNumber}. sezon incelemene yorum yazdı —"
                : "incelemene yorum yazdı —"
        };

        /// <summary>Bildirimin götürdüğü sayfa; hedefi yoksa aktörün profili.</summary>
        public static string TargetUrl(NotificationDto notification)
        {
            if (notification.UserListId.HasValue)
                return $"/list/{notification.UserListId}";

            if (notification.TmdbShowId.HasValue)
                return $"/show/{notification.TmdbShowId}";

            return ProfileUrl(notification.ActorUsername);
        }

        public static string ProfileUrl(string username) => "/" + '@' + username;
    }
}
