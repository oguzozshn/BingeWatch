namespace BingeWatch.API.Dtos
{
    /// <summary>Bir güne ait trafik satırı; paneldeki eğilim grafiği bunları çiziyor.</summary>
    public class DailyTrafficPointDto
    {
        public DateOnly Day { get; set; }
        public long Requests { get; set; }
        public long ResponseBytes { get; set; }
    }

    /// <summary>Admin panelinin üst kısmındaki sayaçlar.</summary>
    public class AdminStatsDto
    {
        // --- Kullanıcılar ---

        /// <summary>Son birkaç dakikada istek atmış kullanıcı sayısı.</summary>
        public int OnlineNow { get; set; }

        /// <summary>"Çevrimiçi" sayılmak için son görülme penceresi (dakika).</summary>
        public int OnlineWindowMinutes { get; set; }

        public int ActiveToday { get; set; }
        public int ActiveYesterday { get; set; }

        /// <summary>Bu ay en az bir kez uygulamayı kullanmış tekil kullanıcı.</summary>
        public int ActiveThisMonth { get; set; }

        /// <summary>Kayıtlı toplam kullanıcı.</summary>
        public int TotalUsers { get; set; }

        /// <summary>Bugün kaydolan.</summary>
        public int NewUsersToday { get; set; }

        // --- Trafik ---

        public long RequestsToday { get; set; }
        public long RequestsThisMonth { get; set; }
        public long RequestsTotal { get; set; }

        public long BytesToday { get; set; }
        public long BytesThisMonth { get; set; }
        public long BytesTotal { get; set; }

        /// <summary>Son 14 gün, eskiden yeniye. Veri olmayan günler sıfırla doldurulur.</summary>
        public List<DailyTrafficPointDto> RecentDays { get; set; } = new();

        // --- İçerik ---

        public int TotalShows { get; set; }
        public int TotalReviews { get; set; }
        public int OpenReports { get; set; }

        /// <summary>Sayaçların hangi ana ait olduğu (UTC) — panelde "son güncelleme".</summary>
        public DateTime GeneratedAt { get; set; }
    }
}
