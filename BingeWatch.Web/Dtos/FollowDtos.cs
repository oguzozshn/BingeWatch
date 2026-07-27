namespace BingeWatch.Web.Dtos
{
    /// <summary>Takipçi / takip edilen listelerindeki kullanıcı kartı.</summary>
    public class UserSummaryDto
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string? Bio { get; set; }

        public bool IsFollowedByViewer { get; set; }
        public bool IsViewer { get; set; }
    }
}
