namespace BingeWatch.Web.Dtos
{
    /// <summary>API'deki <c>ReportTargetType</c> ile aynı sırada olmalı.</summary>
    public enum ReportTargetType
    {
        Review = 1,
        ReviewComment = 2,
        UserList = 3,
        User = 4
    }

    public enum ReportReason
    {
        Spam = 1,
        Harassment = 2,
        UnmarkedSpoiler = 3,
        HatefulContent = 4,
        Other = 5
    }

    public enum ReportStatus
    {
        Open = 1,
        Resolved = 2,
        Dismissed = 3
    }

    public enum ReportAction
    {
        Dismiss = 1,
        DeleteContent = 2
    }

    public class CreateReportRequest
    {
        public ReportTargetType TargetType { get; set; }
        public int? TargetId { get; set; }
        public string? TargetUsername { get; set; }
        public ReportReason Reason { get; set; } = ReportReason.Other;
        public string? Note { get; set; }
    }

    public class ResolveReportRequest
    {
        public ReportAction Action { get; set; } = ReportAction.Dismiss;
        public string? Note { get; set; }
    }

    public class ReportDto
    {
        public int Id { get; set; }
        public ReportTargetType TargetType { get; set; }
        public int? TargetId { get; set; }
        public ReportReason Reason { get; set; }
        public string? Note { get; set; }
        public ReportStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public string ReporterUsername { get; set; } = string.Empty;
        public string TargetUsername { get; set; } = string.Empty;

        public string? ContentExcerpt { get; set; }
        public string? ContentUrl { get; set; }
        public int OtherOpenReportsForTarget { get; set; }

        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedByUsername { get; set; }
        public string? ResolutionNote { get; set; }
    }

    public class BlockedUserDto
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public DateTime BlockedAt { get; set; }
    }

    /// <summary>Bildirim sebeplerinin Türkçe etiketleri; menü ve moderasyon kuyruğu ortak kullanır.</summary>
    public static class ReportLabels
    {
        public static string Reason(ReportReason reason) => reason switch
        {
            ReportReason.Spam => "Spam / reklam",
            ReportReason.Harassment => "Taciz / hakaret",
            ReportReason.UnmarkedSpoiler => "İşaretlenmemiş spoiler",
            ReportReason.HatefulContent => "Nefret söylemi",
            _ => "Diğer"
        };

        public static string Target(ReportTargetType type) => type switch
        {
            ReportTargetType.Review => "İnceleme",
            ReportTargetType.ReviewComment => "Yorum",
            ReportTargetType.UserList => "Liste",
            _ => "Kullanıcı"
        };
    }
}
