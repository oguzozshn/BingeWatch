namespace BingeWatch.API.Models
{
    /// <summary>
    /// İnceleme yorumu. Bilinçli olarak thread'siz ve tek seviye (bkz. ROADMAP §3.C);
    /// yanıt zinciri moderasyon yükünü katlıyor.
    /// </summary>
    public class ReviewComment
    {
        public int Id { get; set; }

        public int ReviewId { get; set; }
        public Review? Review { get; set; }

        public string UserId { get; set; } = string.Empty;
        public AppUser? User { get; set; }

        public string Body { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
