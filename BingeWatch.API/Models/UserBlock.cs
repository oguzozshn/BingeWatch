namespace BingeWatch.API.Models
{
    /// <summary>
    /// Bir kullanıcının başka bir kullanıcıyı engellemesi. Takipten farklı olarak
    /// tek yönlü kaydedilir ama <b>iki yönlü</b> etki eder: engelleyen de engellenen de
    /// diğerinin profilini, listelerini, incelemelerini ve akıştaki olaylarını görmez.
    /// Engel anında iki yöndeki takipler de koparılır.
    /// </summary>
    public class UserBlock
    {
        public int Id { get; set; }

        /// <summary>Engelleyen.</summary>
        public string BlockerId { get; set; } = string.Empty;
        public AppUser? Blocker { get; set; }

        /// <summary>Engellenen.</summary>
        public string BlockedId { get; set; } = string.Empty;
        public AppUser? Blocked { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
