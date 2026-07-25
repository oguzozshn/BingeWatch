namespace BingeWatch.API.Models
{
    /// <summary>
    /// Kullanıcının listesindeki dizi. Satırın varlığı "listemde" anlamına gelir;
    /// ayrıca bir bayrak tutulmaz.
    /// </summary>
    public class UserShow
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public AppUser? User { get; set; }

        public int ShowId { get; set; }
        public Show? Show { get; set; }

        public WatchStatus Status { get; set; } = WatchStatus.PlanToWatch;

        public bool IsFavorite { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
