namespace BingeWatch.API.Models
{
    /// <summary>
    /// Diziyi yayınlayan kanal/platform (HBO, Netflix...). Tür gibi TMDb id'si
    /// birincil anahtar.
    /// </summary>
    public class Network
    {
        /// <summary>TMDb kanal kimliği.</summary>
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>TMDb'ye göreli logo yolu; tam URL değil.</summary>
        public string? LogoPath { get; set; }

        public List<Show> Shows { get; set; } = new();
    }
}
