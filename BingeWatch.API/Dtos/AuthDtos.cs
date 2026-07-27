namespace BingeWatch.API.Dtos
{
    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        public string UsernameOrEmail { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
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

        /// <summary>İsteği yapan kullanıcı bu profili takip ediyor mu? Anonimde <c>false</c>.</summary>
        public bool IsFollowedByViewer { get; set; }

        /// <summary>Profil isteği yapanın kendisine mi ait? Takip butonu gizlenir.</summary>
        public bool IsViewer { get; set; }
    }
}
