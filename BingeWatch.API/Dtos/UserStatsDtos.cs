namespace BingeWatch.API.Dtos
{
    /// <summary>Profil sayfasının istatistik bloğu — kartlar, favori diziler, yıllık özet.</summary>
    public class UserStatsDto
    {
        public string Username { get; set; } = string.Empty;

        public int WatchedEpisodeCount { get; set; }
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
}
