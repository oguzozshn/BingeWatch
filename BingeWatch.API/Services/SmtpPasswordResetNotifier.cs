using BingeWatch.API.Configurations;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BingeWatch.API.Services
{
    /// <summary>
    /// Sıfırlama bağlantısını SMTP ile gönderir.
    /// </summary>
    /// <remarks>
    /// <c>System.Net.Mail.SmtpClient</c> yerine MailKit kullanılıyor; Microsoft
    /// kendi sınıfını yeni geliştirmeler için önermiyor (modern TLS ve kimlik
    /// doğrulama akışlarını desteklemiyor).
    /// <para>
    /// Sağlayıcıdan bağımsız: Gmail, Resend, Brevo ya da yerel sahte bir sunucu
    /// aynı kodla çalışır, yalnızca <see cref="SmtpSettings"/> değişir.
    /// </para>
    /// </remarks>
    public class SmtpPasswordResetNotifier : IPasswordResetNotifier
    {
        private readonly SmtpSettings _settings;
        private readonly ILogger<SmtpPasswordResetNotifier> _logger;

        public SmtpPasswordResetNotifier(
            IOptions<SmtpSettings> settings, ILogger<SmtpPasswordResetNotifier> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public bool IsEnabled => _settings.IsConfigured;

        public async Task SendAsync(string email, string resetUrl, CancellationToken cancellationToken = default)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = "BingeWatch — şifre sıfırlama";

            // Hem düz metin hem HTML: bazı istemciler HTML'i engelliyor, o zaman
            // bağlantı hiç görünmüyor ve kullanıcı sıkışıyor.
            message.Body = new BodyBuilder
            {
                TextBody =
                    "Şifreni sıfırlamak için aşağıdaki bağlantıyı aç:" + Environment.NewLine +
                    resetUrl + Environment.NewLine + Environment.NewLine +
                    "Bu isteği sen yapmadıysan bu e-postayı yok sayabilirsin; şifren değişmez.",
                HtmlBody =
                    $"<p>Şifreni sıfırlamak için aşağıdaki bağlantıya tıkla:</p>" +
                    $"<p><a href=\"{resetUrl}\">Şifremi sıfırla</a></p>" +
                    "<p>Bu isteği sen yapmadıysan bu e-postayı yok sayabilirsin; şifren değişmez.</p>"
            }.ToMessageBody();

            using var client = new SmtpClient();

            try
            {
                // STARTTLS: 587'de bağlantı şifresiz başlayıp TLS'e yükseliyor.
                // Otomatik seçim, 465 gibi baştan TLS isteyen portları da doğru
                // ele alıyor.
                var securityOption = _settings.UseTls
                    ? SecureSocketOptions.StartTlsWhenAvailable
                    : SecureSocketOptions.None;

                await client.ConnectAsync(_settings.Host, _settings.Port, securityOption, cancellationToken);

                if (!string.IsNullOrWhiteSpace(_settings.User))
                    await client.AuthenticateAsync(_settings.User, _settings.Password, cancellationToken);

                await client.SendAsync(message, cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);

                _logger.LogInformation("Sifre sifirlama e-postasi gonderildi: {Email}", email);
            }
            catch (Exception ex)
            {
                // Bağlantı ya da kimlik hatası kullanıcıya yansıtılmıyor: uç her
                // durumda aynı yanıtı veriyor (hesap sayımına kapalı). Ama
                // sessizce yutulmamalı, yoksa "mail gelmiyor" şikâyetinin
                // sebebi hiçbir yerde görünmez.
                //
                // Bağlantının kendisi loglanmıyor — hesap ele geçirmeye yeter.
                _logger.LogError(ex, "Sifre sifirlama e-postasi gonderilemedi: {Email}", email);
                throw;
            }
        }
    }
}
