namespace BingeWatch.API.Models
{
    /// <summary>
    /// Tek bir bölümün izlendiği kaydı. Yeniden izlemeler <see cref="RewatchNo"/>
    /// artırılarak ayrı satır olarak tutulur (0 = ilk izleme).
    /// </summary>
    public class WatchedEpisode
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public AppUser? User { get; set; }

        public int EpisodeId { get; set; }
        public Episode? Episode { get; set; }

        public DateTime WatchedAt { get; set; } = DateTime.UtcNow;

        public int RewatchNo { get; set; }
    }
}
