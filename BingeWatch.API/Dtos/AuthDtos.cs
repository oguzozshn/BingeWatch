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

    public class ForgotPasswordRequest
    {
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Sıfırlama sayfasının Web tarafındaki adresi (ör.
        /// <c>https://site/reset-password</c>). API kendi adresini bilir ama
        /// kullanıcının tıklayacağı sayfa Web'de; adres oradan geliyor.
        /// </summary>
        public string ResetUrlBase { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    /// <summary>
    /// Giriş yapmış kullanıcının kendi şifresini değiştirmesi. Mevcut şifre
    /// bilerek isteniyor: oturumu ele geçiren birinin şifreyi değiştirip
    /// sahibini dışarıda bırakması ancak böyle pahalılaşır.
    /// </summary>
    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Identity rolleri; şimdilik yalnızca "Admin" olabiliyor.</summary>
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

        /// <summary>İsteği yapan kullanıcı bu profili takip ediyor mu? Anonimde <c>false</c>.</summary>
        public bool IsFollowedByViewer { get; set; }

        /// <summary>Profil isteği yapanın kendisine mi ait? Takip butonu gizlenir.</summary>
        public bool IsViewer { get; set; }

        /// <summary>
        /// Yalnızca sahibi kendi profilini okurken dolu; başkasının gizlilik
        /// tercihi kimseyi ilgilendirmiyor ve gizli profil zaten hiç dönmüyor.
        /// </summary>
        public bool? IsPrivate { get; set; }
    }

    /// <summary>
    /// Profil düzenleme. Kullanıcı adı ve e-posta bilerek dışarıda: ikisi de
    /// kimlik alanı, değiştirilmeleri ayrı akışlar (adres doğrulama, eski
    /// bağlantıların kırılması) gerektirir.
    /// </summary>
    public class UpdateProfileRequest
    {
        public string? DisplayName { get; set; }
        public string? Bio { get; set; }
        public string? AvatarUrl { get; set; }
        public bool IsPrivate { get; set; }
    }
}
