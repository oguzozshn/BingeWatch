namespace BingeWatch.API.Dtos
{
    /// <summary>Profil sayfasının istatistik bloğu — kartlar, favori diziler, yıllık özet.</summary>
    public class UserStatsDto
    {
        public string Username { get; set; } = string.Empty;

        public int WatchedEpisodeCount { get; set; }

        /// <summary>İlk izlemeler hariç, toplam yeniden izleme sayısı.</summary>
        public int RewatchCount { get; set; }

        public int ShowsWatchingCount { get; set; }
        public int ShowsCompletedCount { get; set; }
        public int ReviewCount { get; set; }
        public int RatingCount { get; set; }

        /// <summary>Kullanıcının dizi seviyesindeki puanlarının ortalaması.</summary>
        public double? AverageRating { get; set; }

        /// <summary>Toplam izlenen dakika — bölüm süresi bilinmeyenler sayılmaz.</summary>
        public int TotalMinutes { get; set; }

        /// <summary>Favori olarak işaretlenen diziler (profilde ilk dördü gösterilir).</summary>
        public List<FavoriteShowDto> FavoriteShows { get; set; } = new();

        /// <summary>Yıl → o yıl izlenen bölüm sayısı, eskiden yeniye.</summary>
        public List<YearlyCountDto> YearlyCounts { get; set; } = new();
    }

    public class FavoriteShowDto
    {
        public int TmdbId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
    }

    public class SetFavoriteRequest
    {
        public bool IsFavorite { get; set; }
    }

    public class YearlyCountDto
    {
        public int Year { get; set; }
        public int EpisodeCount { get; set; }
    }

    /// <summary>
    /// İstatistik sayfasının tamamı. Profil bloğundan ayrı tutuluyor: profilde
    /// gereksiz olan ağır sorgular (tür dağılımı, en çok izlenenler) yalnızca
    /// bu sayfa açıldığında çalışsın.
    /// </summary>
    public class UserStatsDetailDto
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Kaç farklı bölüm izlendi — yeniden izlemeler tekrar saymaz.</summary>
        public int WatchedEpisodeCount { get; set; }

        /// <summary>İlk izlemeler hariç, toplam yeniden izleme sayısı.</summary>
        public int RewatchCount { get; set; }

        /// <summary>
        /// Toplam izlenen dakika — süresi bilinmeyen bölümler sayılmaz,
        /// yeniden izlemeler sayılır.
        /// </summary>
        public int TotalMinutes { get; set; }

        /// <summary>Süresi bilinmeyen bölüm sayısı; toplam sürenin eksik olduğunu dürüstçe göstermek için.</summary>
        public int EpisodesWithoutRuntime { get; set; }

        public int ShowCount { get; set; }
        public int ShowsWatchingCount { get; set; }
        public int ShowsCompletedCount { get; set; }
        public int ShowsDroppedCount { get; set; }
        public int ShowsPlannedCount { get; set; }
        public int ShowsOnHoldCount { get; set; }

        public int ReviewCount { get; set; }
        public int RatingCount { get; set; }
        public double? AverageRating { get; set; }

        public List<YearlyStatDto> Yearly { get; set; } = new();
        public List<GenreStatDto> Genres { get; set; } = new();
        public List<TopShowDto> TopShows { get; set; } = new();

        /// <summary>0,5–5 arası on kova; boş kovalar 0 olarak döner.</summary>
        public List<RatingBucketDto> RatingDistribution { get; set; } = new();
    }

    public class YearlyStatDto
    {
        public int Year { get; set; }
        public int EpisodeCount { get; set; }
        public int Minutes { get; set; }
    }

    public class GenreStatDto
    {
        public int GenreId { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>O türdeki dizilerden izlenen bölüm sayısı.</summary>
        public int EpisodeCount { get; set; }

        /// <summary>Kullanıcının o türde dokunduğu dizi sayısı.</summary>
        public int ShowCount { get; set; }
    }

    public class TopShowDto
    {
        public int TmdbId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
        public int EpisodeCount { get; set; }
        public int Minutes { get; set; }
    }

    public class RatingBucketDto
    {
        /// <summary>Kovanın puanı: 0,5 / 1 / ... / 5.</summary>
        public decimal Value { get; set; }
        public int Count { get; set; }
    }
}
