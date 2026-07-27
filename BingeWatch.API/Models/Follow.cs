namespace BingeWatch.API.Models
{
    /// <summary>
    /// Tek yönlü takip ilişkisi (Letterboxd modeli): <see cref="FollowerId"/> kullanıcısı
    /// <see cref="FolloweeId"/> kullanıcısını takip eder. Karşılıklılık gerekmez.
    /// </summary>
    public class Follow
    {
        public int Id { get; set; }

        /// <summary>Takip eden.</summary>
        public string FollowerId { get; set; } = string.Empty;

        /// <summary>Takip edilen.</summary>
        public string FolloweeId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public AppUser? Follower { get; set; }
        public AppUser? Followee { get; set; }
    }
}
