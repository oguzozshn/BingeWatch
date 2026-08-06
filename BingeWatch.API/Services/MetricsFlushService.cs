using BingeWatch.API.Data;
using BingeWatch.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BingeWatch.API.Services
{
    /// <summary>
    /// <see cref="RequestMetricsCollector"/>'da biriken sayaçları periyodik olarak
    /// veritabanına yazar. <c>BingeOnDbContext</c> scoped olduğu için her turda
    /// kendi scope'unu açar (bu servis singleton'dır).
    /// </summary>
    public class MetricsFlushService : BackgroundService
    {
        /// <summary>
        /// Yazma aralığı. Kısaltmak "şu an çevrimiçi" sayacını tazeler ama
        /// veritabanına daha sık yazar; uzatmak tersi. 30 saniye, panelin
        /// canlı hissettirmesi için yeterince sık.
        /// </summary>
        private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RequestMetricsCollector _collector;
        private readonly ILogger<MetricsFlushService> _logger;

        public MetricsFlushService(
            IServiceScopeFactory scopeFactory,
            RequestMetricsCollector collector,
            ILogger<MetricsFlushService> logger)
        {
            _scopeFactory = scopeFactory;
            _collector = collector;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(FlushInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                await FlushAsync(stoppingToken);
            }

            // Kapanırken son aralığı da yaz — aksi halde her yeniden başlatmada
            // son 30 saniyenin sayaçları kaybolurdu. Kapanış iptal jetonu zaten
            // iptal edilmiş olduğu için burada None geçiliyor.
            await FlushAsync(CancellationToken.None);
        }

        private async Task FlushAsync(CancellationToken cancellationToken)
        {
            var snapshot = _collector.Drain();
            if (snapshot.IsEmpty)
                return;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<BingeOnDbContext>();

                var today = DateOnly.FromDateTime(DateTime.UtcNow);

                await WriteTrafficAsync(context, today, snapshot, cancellationToken);
                await WriteActiveUsersAsync(context, today, snapshot, cancellationToken);

                await context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Metrik yazımının hatası uygulamayı etkilememeli; sayaçlar zaten
                // boşaltıldığı için bu aralık kaybolur, bir sonraki tur devam eder.
                _logger.LogError(ex, "Metrikler yazılamadı");
            }
        }

        private static async Task WriteTrafficAsync(
            BingeOnDbContext context, DateOnly today, MetricsSnapshot snapshot, CancellationToken cancellationToken)
        {
            if (snapshot.Requests == 0)
                return;

            var row = await context.DailyTrafficStats
                .FirstOrDefaultAsync(s => s.Day == today, cancellationToken);

            if (row == null)
            {
                context.DailyTrafficStats.Add(new DailyTrafficStat
                {
                    Day = today,
                    Requests = snapshot.Requests,
                    ResponseBytes = snapshot.ResponseBytes
                });
                return;
            }

            row.Requests += snapshot.Requests;
            row.ResponseBytes += snapshot.ResponseBytes;
        }

        private static async Task WriteActiveUsersAsync(
            BingeOnDbContext context, DateOnly today, MetricsSnapshot snapshot, CancellationToken cancellationToken)
        {
            if (snapshot.UserIds.Count == 0)
                return;

            var userIds = snapshot.UserIds.ToList();

            // Bugün zaten kaydedilmiş olanları çıkar; kalanlar için tek satır aç.
            var alreadyRecorded = await context.DailyActiveUsers
                .Where(a => a.Day == today && userIds.Contains(a.UserId))
                .Select(a => a.UserId)
                .ToListAsync(cancellationToken);

            foreach (var userId in userIds.Except(alreadyRecorded))
                context.DailyActiveUsers.Add(new DailyActiveUser { Day = today, UserId = userId });

            // "Şu an çevrimiçi" sayımı LastSeenAt'e bakıyor; bu aralıkta istek atan
            // herkesin damgası tazeleniyor.
            var seenAt = DateTime.UtcNow;
            await context.Users
                .Where(u => userIds.Contains(u.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.LastSeenAt, seenAt), cancellationToken);
        }
    }
}
