using BingeWatch.API.Models;

namespace BingeWatch.API.Dtos
{
    /// <summary>Engelleme/engel kaldırma sonucu; controller bunu HTTP durumuna çevirir.</summary>
    public enum BlockResult
    {
        Ok,
        TargetNotFound,

        /// <summary>Kendini engelleyemezsin.</summary>
        Self
    }

    /// <summary>Engellenenler listesindeki satır.</summary>
    public class BlockedUserDto
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public DateTime BlockedAt { get; set; }
    }

    public class CreateReportRequest
    {
        public ReportTargetType TargetType { get; set; }

        /// <summary>İçerik id'si; kullanıcı bildiriminde boş bırakılır.</summary>
        public int? TargetId { get; set; }

        /// <summary>Kullanıcı bildiriminde hedefin kullanıcı adı.</summary>
        public string? TargetUsername { get; set; }

        public ReportReason Reason { get; set; } = ReportReason.Other;

        public string? Note { get; set; }
    }

    /// <summary>Bildirim oluşturma sonucu.</summary>
    public enum ReportResult
    {
        Ok,

        /// <summary>Aynı hedef için zaten açık bir bildirimin var; ikincisi kuyruğu şişirir.</summary>
        AlreadyReported,

        TargetNotFound,

        /// <summary>Kendi içeriğini bildirmek anlamsız.</summary>
        Self
    }

    /// <summary>Moderasyon kuyruğundaki bildirim.</summary>
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

        /// <summary>Bildirilen içeriğin metni; içerik silinmişse <c>null</c>.</summary>
        public string? ContentExcerpt { get; set; }

        /// <summary>İçeriğe gidecek uygulama içi bağlantı; hedef silinmişse <c>null</c>.</summary>
        public string? ContentUrl { get; set; }

        /// <summary>Aynı kullanıcı için açık kalan diğer bildirimlerin sayısı.</summary>
        public int OtherOpenReportsForTarget { get; set; }

        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedByUsername { get; set; }
        public string? ResolutionNote { get; set; }
    }

    /// <summary>Moderatörün bildirimi kapatırken seçtiği eylem.</summary>
    public enum ReportAction
    {
        /// <summary>Bildirim haksız; içerik yerinde kalır.</summary>
        Dismiss = 1,

        /// <summary>İçerik kaldırılır ve bildirim kapatılır.</summary>
        DeleteContent = 2
    }

    public class ResolveReportRequest
    {
        public ReportAction Action { get; set; } = ReportAction.Dismiss;
        public string? Note { get; set; }
    }
}
