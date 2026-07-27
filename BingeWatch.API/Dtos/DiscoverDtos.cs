using BingeWatch.API.Models;

namespace BingeWatch.API.Dtos
{
    /// <summary>Keşif filtresi. Boş bırakılan alan filtrelemez.</summary>
    public class DiscoverQuery
    {
        /// <summary>TMDb tür kimlikleri; birden fazlası "ve" ile birleşir.</summary>
        public List<int> GenreIds { get; set; } = new();

        /// <summary>TMDb kanal/platform kimlikleri; birden fazlası "veya" ile birleşir.</summary>
        public List<int> NetworkIds { get; set; } = new();

        public int? YearFrom { get; set; }
        public int? YearTo { get; set; }

        /// <summary>TMDb oy ortalaması alt sınırı (0–10).</summary>
        public double? MinRating { get; set; }

        public DiscoverSort Sort { get; set; } = DiscoverSort.Popularity;

        /// <summary>
        /// Doluysa arama yerel kütüphanede yapılır: yalnızca isteği yapanın bu
        /// durumdaki dizileri döner. Kütüphane modunda TMDb'ye gidilmez.
        /// </summary>
        public WatchStatus? Status { get; set; }

        public int Page { get; set; } = 1;
    }

    public enum DiscoverSort
    {
        Popularity = 0,
        Rating = 1,
        Newest = 2
    }

    public class DiscoverResultDto
    {
        public int Page { get; set; }
        public List<DiscoverShowDto> Results { get; set; } = new();

        /// <summary>Sonraki sayfa var mı? TMDb toplam sayfayı vermediğinde dolu sayfaya bakılır.</summary>
        public bool HasMore { get; set; }
    }

    public class DiscoverShowDto
    {
        public int TmdbId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
        public int? FirstAirYear { get; set; }
        public double VoteAverage { get; set; }

        /// <summary>Kütüphane modunda kullanıcının durumu; keşif modunda <c>null</c>.</summary>
        public WatchStatus? Status { get; set; }
    }

    public class GenreDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class NetworkDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Gelişmiş arama sonucu: diziler ve kişiler bir arada.</summary>
    public class SearchResultDto
    {
        public List<DiscoverShowDto> Shows { get; set; } = new();
        public List<PersonDto> People { get; set; } = new();
    }

    public class PersonDto
    {
        public int TmdbId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ProfilePath { get; set; }

        /// <summary>"Acting", "Directing"... — kişiyi ayırt etmeye yardım eder.</summary>
        public string? KnownForDepartment { get; set; }

        /// <summary>En bilinen birkaç dizisinin adı; arama sonucunda ipucu olarak gösterilir.</summary>
        public List<string> KnownForShows { get; set; } = new();
    }

    /// <summary>Bir kişinin dizileri — kişi sayfasında listelenir.</summary>
    public class PersonCreditsDto
    {
        public int TmdbId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ProfilePath { get; set; }
        public List<PersonCreditDto> Credits { get; set; } = new();
    }

    public class PersonCreditDto
    {
        public int TmdbShowId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
        public int? FirstAirYear { get; set; }
        public double VoteAverage { get; set; }

        /// <summary>Karakter adı ya da görev ("Director"); hangisi doluysa o.</summary>
        public string? Role { get; set; }

        public int EpisodeCount { get; set; }
    }
}
