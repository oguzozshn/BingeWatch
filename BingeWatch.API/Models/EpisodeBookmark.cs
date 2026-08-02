namespace BingeWatch.API.Models
{
    /// <summary>
    /// Yarıda bırakılan bir bölümde nerede kalındığı. Bölüm başına en fazla bir
    /// satır: "kaldığım yer" tek bir noktadır, geçmişi tutulmaz.
    ///
    /// <see cref="WatchedEpisode"/>'dan ayrı bir varlık olmasının sebebi, ikisinin
    /// birbirini dışlaması: bölüm izlendiyse yarıda kalmış olamaz. Aynı tabloya
    /// nullable bir kolon olarak eklenseydi "izlendi ama 32. dakikada" gibi
    /// anlamsız bir satır mümkün olurdu.
    /// </summary>
    public class EpisodeBookmark
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public AppUser? User { get; set; }

        public int EpisodeId { get; set; }
        public Episode? Episode { get; set; }

        /// <summary>Bölümün başından itibaren kaçıncı dakikada kalındığı.</summary>
        public int PositionMinutes { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
