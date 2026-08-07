namespace BingeWatch.API.Dtos
{
    /// <summary>
    /// "Hangisinin puanı yüksek?" oyununda tek bir el: iki dizi ve doğru cevap.
    ///
    /// Puanlar yanıtta açıkça dönüyor. Blazor Server'da bileşen durumu sunucuda
    /// yaşadığı için tarayıcıya sızmıyorlar; zaten TMDb'de herkese açık veri,
    /// oyunun amacı sıralama bilgisini eğlenceli hale getirmek.
    /// </summary>
    public class GameRoundDto
    {
        public GameContenderDto Left { get; set; } = new();
        public GameContenderDto Right { get; set; } = new();

        /// <summary>Puanı yüksek olanın TMDb kimliği.</summary>
        public int WinnerTmdbId { get; set; }

        /// <summary>İki puan eşitse her iki cevap da doğru sayılır.</summary>
        public bool IsTie { get; set; }
    }

    public class GameContenderDto
    {
        public int TmdbId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
        public int? FirstAirYear { get; set; }
        public double VoteAverage { get; set; }
    }
}
