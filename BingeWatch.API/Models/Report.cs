namespace BingeWatch.API.Models
{
    public enum ReportTargetType
    {
        Review = 1,
        ReviewComment = 2,
        UserList = 3,

        /// <summary>Tek bir içerik değil, kullanıcının kendisi bildirilir.</summary>
        User = 4
    }

    public enum ReportReason
    {
        Spam = 1,
        Harassment = 2,

        /// <summary>Spoiler bayrağı olmadan spoiler içeriyor.</summary>
        UnmarkedSpoiler = 3,

        HatefulContent = 4,
        Other = 5
    }

    public enum ReportStatus
    {
        Open = 1,

        /// <summary>Moderatör içeriği kaldırdı.</summary>
        Resolved = 2,

        /// <summary>Moderatör bildirimi haksız buldu.</summary>
        Dismissed = 3
    }

    /// <summary>
    /// Kullanıcıdan gelen içerik bildirimi. <see cref="TargetId"/> polimorfiktir
    /// (inceleme / yorum / liste id'si), kullanıcı bildiriminde <c>null</c> kalır.
    /// <see cref="TargetUserId"/> her durumda doludur: içeriğin sahibi, moderasyon
    /// panelinde "aynı kullanıcıdan kaç bildirim var" sorusunu içerik silinse bile
    /// cevaplayabilsin diye kopyalanır.
    /// </summary>
    public class Report
    {
        public int Id { get; set; }

        public string ReporterId { get; set; } = string.Empty;
        public AppUser? Reporter { get; set; }

        public ReportTargetType TargetType { get; set; }

        /// <summary>İçerik id'si; <see cref="ReportTargetType.User"/> bildiriminde <c>null</c>.</summary>
        public int? TargetId { get; set; }

        /// <summary>Bildirilen içeriğin sahibi ya da bildirilen kullanıcı.</summary>
        public string TargetUserId { get; set; } = string.Empty;
        public AppUser? TargetUser { get; set; }

        public ReportReason Reason { get; set; }

        /// <summary>Bildirenin serbest açıklaması.</summary>
        public string? Note { get; set; }

        public ReportStatus Status { get; set; } = ReportStatus.Open;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Bildirimi kapatan moderatör.</summary>
        public string? ResolvedById { get; set; }
        public AppUser? ResolvedBy { get; set; }

        public DateTime? ResolvedAt { get; set; }

        /// <summary>Moderatörün kapatma notu.</summary>
        public string? ResolutionNote { get; set; }
    }
}
