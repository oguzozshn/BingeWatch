namespace BingeWatch.Web.Models
{
    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>API'den gelen roller; cookie'ye rol claim'i olarak yazılır.</summary>
        public List<string> Roles { get; set; } = new();
    }

    public class UserProfileDto
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? AvatarUrl { get; set; }
        public DateTime CreatedAt { get; set; }

        public int FollowerCount { get; set; }
        public int FollowingCount { get; set; }

        public bool IsFollowedByViewer { get; set; }
        public bool IsViewer { get; set; }

        /// <summary>Yalnızca sahibi kendi profilini okurken dolu.</summary>
        public bool? IsPrivate { get; set; }
    }

    public class UpdateProfileRequest
    {
        public string? DisplayName { get; set; }
        public string? Bio { get; set; }
        public string? AvatarUrl { get; set; }
        public bool IsPrivate { get; set; }
    }
}
