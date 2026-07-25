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
    }
}
