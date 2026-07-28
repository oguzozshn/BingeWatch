namespace BingeWatch.API.Configurations
{
    /// <summary>
    /// Giden e-posta ayarları. Sağlayıcıdan bağımsız: Gmail, Resend, Brevo ya da
    /// yerel bir sahte SMTP sunucusu — hepsi aynı alanlarla çalışır, yalnızca
    /// değerler değişir.
    /// </summary>
    public class SmtpSettings
    {
        public string? Host { get; set; }

        /// <summary>587 = STARTTLS (yaygın), 465 = baştan TLS, 25 = şifresiz.</summary>
        public int Port { get; set; } = 587;

        public string? User { get; set; }

        /// <summary>
        /// Gmail'de bu <b>hesap parolası değil</b>, 16 haneli uygulama şifresidir.
        /// Sır olduğu için yapılandırma dosyasına değil, user-secrets ya da
        /// ortam değişkenine (<c>Smtp__Password</c>) yazılmalı.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>Gönderen adres. Gmail'de kimlik doğrulanan adresle aynı olmalı.</summary>
        public string? FromAddress { get; set; }

        public string FromName { get; set; } = "BingeWatch";

        /// <summary>
        /// Yerelde sahte SMTP sunucularının çoğu TLS konuşmuyor; yalnızca
        /// geliştirme için kapatılabilsin diye ayrı bayrak.
        /// </summary>
        public bool UseTls { get; set; } = true;

        /// <summary>
        /// Gönderim için yeterli bilgi var mı? Kullanıcı adı/parola bilerek
        /// aranmıyor: kimlik doğrulaması istemeyen (yerel, kurum içi) sunucular
        /// da geçerli bir kurulum.
        /// </summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
    }
}
