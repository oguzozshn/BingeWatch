using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BingeWatch.API.Services
{
    public interface IAdminStatsService
    {
        Task<AdminStatsDto> GetAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Admin panelindeki sayaçları toplar. Hepsi tek tek sorgu; panel elle
    /// yenilenen bir sayfa, dakikada bir çağrılmıyor.
    /// </summary>
    public class AdminStatsService : IAdminStatsService
    {
        /// <summary>
        /// Bu süre içinde istek atmış kullanıcı "çevrimiçi" sayılır. Metrikler 30
        /// saniyede bir yazıldığı için pencere ondan belirgin şekilde geniş olmalı;
        /// 5 dakika, sayfayı açık tutup okuyan kullanıcıyı da çevrimiçi sayar.
        /// </summary>
        private const int OnlineWindowMinutes = 5;

        /// <summary>Eğilim grafiğinin genişliği.</summary>
        private const int RecentDayCount = 14;

        private readonly BingeOnDbContext _context;

        public AdminStatsService(BingeOnDbContext context)
        {
            _context = context;
        }

        public async Task<AdminStatsDto> GetAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);
            var yesterday = today.AddDays(-1);
            var monthStart = new DateOnly(today.Year, today.Month, 1);
            var windowStart = now.AddMinutes(-OnlineWindowMinutes);
            var firstRecentDay = today.AddDays(-(RecentDayCount - 1));

            var stats = new AdminStatsDto
            {
                OnlineWindowMinutes = OnlineWindowMinutes,
                GeneratedAt = now
            };

            stats.OnlineNow = await _context.Users
                .CountAsync(u => u.LastSeenAt != null && u.LastSeenAt >= windowStart, cancellationToken);

            stats.ActiveToday = await _context.DailyActiveUsers
                .CountAsync(a => a.Day == today, cancellationToken);

            stats.ActiveYesterday = await _context.DailyActiveUsers
                .CountAsync(a => a.Day == yesterday, cancellationToken);

            // Ay içinde farklı günlerde giren aynı kullanıcı bir kez sayılmalı.
            stats.ActiveThisMonth = await _context.DailyActiveUsers
                .Where(a => a.Day >= monthStart)
                .Select(a => a.UserId)
                .Distinct()
                .CountAsync(cancellationToken);

            stats.TotalUsers = await _context.Users.CountAsync(cancellationToken);

            var todayStart = today.ToDateTime(TimeOnly.MinValue);
            stats.NewUsersToday = await _context.Users
                .CountAsync(u => u.CreatedAt >= todayStart, cancellationToken);

            await FillTrafficAsync(stats, today, monthStart, firstRecentDay, cancellationToken);

            stats.TotalShows = await _context.Shows.CountAsync(cancellationToken);
            stats.TotalReviews = await _context.Reviews.CountAsync(cancellationToken);
            stats.OpenReports = await _context.Reports
                .CountAsync(r => r.Status == ReportStatus.Open, cancellationToken);

            return stats;
        }

        private async Task FillTrafficAsync(
            AdminStatsDto stats, DateOnly today, DateOnly monthStart, DateOnly firstRecentDay,
            CancellationToken cancellationToken)
        {
            var todayRow = await _context.DailyTrafficStats
                .FirstOrDefaultAsync(s => s.Day == today, cancellationToken);

            stats.RequestsToday = todayRow?.Requests ?? 0;
            stats.BytesToday = todayRow?.ResponseBytes ?? 0;

            var month = await _context.DailyTrafficStats
                .Where(s => s.Day >= monthStart)
                .GroupBy(_ => 1)
                .Select(g => new { Requests = g.Sum(s => s.Requests), Bytes = g.Sum(s => s.ResponseBytes) })
                .FirstOrDefaultAsync(cancellationToken);

            stats.RequestsThisMonth = month?.Requests ?? 0;
            stats.BytesThisMonth = month?.Bytes ?? 0;

            var total = await _context.DailyTrafficStats
                .GroupBy(_ => 1)
                .Select(g => new { Requests = g.Sum(s => s.Requests), Bytes = g.Sum(s => s.ResponseBytes) })
                .FirstOrDefaultAsync(cancellationToken);

            stats.RequestsTotal = total?.Requests ?? 0;
            stats.BytesTotal = total?.Bytes ?? 0;

            var recent = await _context.DailyTrafficStats
                .Where(s => s.Day >= firstRecentDay)
                .ToDictionaryAsync(s => s.Day, cancellationToken);

            // Trafiği olmayan günler tabloda yok; grafiğin ekseni kaymasın diye
            // sıfırla dolduruluyor.
            for (var day = firstRecentDay; day <= today; day = day.AddDays(1))
            {
                recent.TryGetValue(day, out var row);
                stats.RecentDays.Add(new DailyTrafficPointDto
                {
                    Day = day,
                    Requests = row?.Requests ?? 0,
                    ResponseBytes = row?.ResponseBytes ?? 0
                });
            }
        }
    }
}
