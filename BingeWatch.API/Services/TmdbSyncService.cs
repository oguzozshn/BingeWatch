namespace BingeWatch.API.Services
{
    /// <summary>
    /// Devam eden dizilerin yeni bölümlerini periyodik olarak günceller.
    /// <see cref="IShowCatalogService"/> scoped olduğu için her turda kendi
    /// scope'unu açar (bu servis singleton'dır).
    /// </summary>
    public class TmdbSyncService : BackgroundService
    {
        private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(6);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TmdbSyncService> _logger;

        public TmdbSyncService(IServiceScopeFactory scopeFactory, ILogger<TmdbSyncService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // İlk turu uygulama tam ayağa kalktıktan hemen sonra çalıştır.
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunSyncCycleAsync(stoppingToken);

                try
                {
                    await Task.Delay(SyncInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Uygulama kapanıyor — döngüyü sonlandır.
                }
            }
        }

        private async Task RunSyncCycleAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var catalogService = scope.ServiceProvider.GetRequiredService<IShowCatalogService>();

            try
            {
                var syncedCount = await catalogService.SyncStaleOngoingShowsAsync(stoppingToken);
                if (syncedCount > 0)
                    _logger.LogInformation("TmdbSyncService: {Count} dizi güncellendi", syncedCount);
            }
            catch (Exception ex)
            {
                // Bir senkron turunun hatası servisin bir daha hiç çalışmamasına yol açmamalı.
                _logger.LogError(ex, "TmdbSyncService senkron turu başarısız oldu");
            }
        }
    }
}
