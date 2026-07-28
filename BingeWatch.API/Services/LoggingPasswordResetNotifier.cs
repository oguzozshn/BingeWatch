namespace BingeWatch.API.Services
{
    /// <summary>
    /// Sıfırlama bağlantısını loga yazar.
    /// </summary>
    /// <remarks>
    /// <b>Yalnızca Development içindir.</b> Bağlantı, hesabın parolasını
    /// değiştirmeye yeten bir sırdır; loga yazmak onu logu okuyabilen herkese
    /// açar. Üretimde bu uygulama kayıtlı olursa sıfırlama bağlantıları
    /// kullanıcıya hiç ulaşmaz ve log dosyasında birikir — bu yüzden
    /// <c>Program.cs</c> onu yalnızca Development'ta bağlıyor ve üretimde
    /// gerçek bir gönderici yoksa açılışta hata veriyor.
    /// </remarks>
    public class LoggingPasswordResetNotifier : IPasswordResetNotifier
    {
        private readonly ILogger<LoggingPasswordResetNotifier> _logger;

        public LoggingPasswordResetNotifier(ILogger<LoggingPasswordResetNotifier> logger)
        {
            _logger = logger;
        }

        public bool IsEnabled => true;

        public Task SendAsync(string email, string resetUrl, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning(
                "[GELISTIRME] {Email} icin sifre sifirlama baglantisi: {ResetUrl}",
                email, resetUrl);

            return Task.CompletedTask;
        }
    }
}
