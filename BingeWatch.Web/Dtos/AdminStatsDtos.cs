namespace BingeWatch.Web.Dtos
{
    /// <summary>API'deki <c>DailyTrafficPointDto</c> ile aynı alanlar.</summary>
    public class DailyTrafficPointDto
    {
        public DateOnly Day { get; set; }
        public long Requests { get; set; }
        public long ResponseBytes { get; set; }
    }

    /// <summary>API'deki <c>AdminStatsDto</c> ile aynı alanlar.</summary>
    public class AdminStatsDto
    {
        public int OnlineNow { get; set; }
        public int OnlineWindowMinutes { get; set; }
        public int ActiveToday { get; set; }
        public int ActiveYesterday { get; set; }
        public int ActiveThisMonth { get; set; }
        public int TotalUsers { get; set; }
        public int NewUsersToday { get; set; }

        public long RequestsToday { get; set; }
        public long RequestsThisMonth { get; set; }
        public long RequestsTotal { get; set; }

        public long BytesToday { get; set; }
        public long BytesThisMonth { get; set; }
        public long BytesTotal { get; set; }

        public List<DailyTrafficPointDto> RecentDays { get; set; } = new();

        public int TotalShows { get; set; }
        public int TotalReviews { get; set; }
        public int OpenReports { get; set; }

        public DateTime GeneratedAt { get; set; }
    }

    /// <summary><c>GET /api/admin/logs</c> yanıtı.</summary>
    public class AdminLogsDto
    {
        public List<string> Lines { get; set; } = new();
    }
}
