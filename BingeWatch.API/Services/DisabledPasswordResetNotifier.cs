namespace BingeWatch.API.Services
{
    /// <summary>
    /// Teslimat yapılandırılmamış: şifre sıfırlama kapalı.
    /// </summary>
    /// <remarks>
    /// Development dışında kayıtlı olan uygulama budur, çünkü projede henüz
    /// gerçek bir gönderici (SMTP vb.) yok. Sessizce "gönderdik" demek yerine
    /// <see cref="IsEnabled"/> ile kapalı olduğunu bildiriyor; uç 503 dönüyor ve
    /// Web bağlantıyı hiç göstermiyor.
    /// <para>
    /// Gerçek gönderici eklendiğinde <c>Program.cs</c>'te bunun yerine o
    /// kaydedilir; başka hiçbir yer değişmez.
    /// </para>
    /// </remarks>
    public class DisabledPasswordResetNotifier : IPasswordResetNotifier
    {
        private readonly ILogger<DisabledPasswordResetNotifier> _logger;

        public DisabledPasswordResetNotifier(ILogger<DisabledPasswordResetNotifier> logger)
        {
            _logger = logger;
        }

        public bool IsEnabled => false;

        public Task SendAsync(string email, string resetUrl, CancellationToken cancellationToken = default)
        {
            // Buraya gelinmemeli; uç IsEnabled'a bakıp önce 503 dönüyor.
            // Yine de gelinirse sebebi bilinsin — ama bağlantı loga yazılmıyor.
            _logger.LogError(
                "Sifre sifirlama istendi ama teslimat yapilandirilmamis. " +
                "Gercek bir IPasswordResetNotifier kaydedilmeli.");

            return Task.CompletedTask;
        }
    }
}
