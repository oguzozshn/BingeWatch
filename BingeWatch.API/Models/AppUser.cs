using Microsoft.AspNetCore.Identity;

namespace BingeWatch.API.Models
{
    public class AppUser : IdentityUser
    {
        public string DisplayName { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? AvatarUrl { get; set; }
        public bool IsPrivate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Son API isteğinin zamanı (UTC); admin panelindeki "şu an çevrimiçi"
        /// sayımı buna bakıyor. Her istekte tek tek değil, metrik yazımıyla
        /// birlikte toplu güncelleniyor — sayaç en fazla bir yazma aralığı
        /// kadar geride kalır.
        /// </summary>
        public DateTime? LastSeenAt { get; set; }
    }
}
