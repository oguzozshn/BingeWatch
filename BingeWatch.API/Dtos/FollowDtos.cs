namespace BingeWatch.API.Dtos
{
    /// <summary>Takip listelerinde ve ileride aktivite akışında kullanılan kısa kullanıcı kartı.</summary>
    public class UserSummaryDto
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string? Bio { get; set; }

        /// <summary>İsteği yapan kullanıcı bu kişiyi takip ediyor mu? Anonimde her zaman <c>false</c>.</summary>
        public bool IsFollowedByViewer { get; set; }

        /// <summary>Listedeki kişi isteği yapanın kendisi mi? Takip butonu bu satırda gizlenir.</summary>
        public bool IsViewer { get; set; }
    }
}
