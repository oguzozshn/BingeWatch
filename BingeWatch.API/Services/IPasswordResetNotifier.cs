namespace BingeWatch.API.Services
{
    /// <summary>
    /// Şifre sıfırlama bağlantısını kullanıcıya ulaştırır.
    /// </summary>
    /// <remarks>
    /// Teslimat bilerek soyutlandı: projede e-posta altyapısı yok ve onu
    /// beklemek akışın tamamını (uçlar, token üretimi, sayfalar) rehin alırdı.
    /// Development'ta <see cref="LoggingPasswordResetNotifier"/> bağlantıyı loga
    /// yazıyor; üretim için SMTP uygulaması bu arayüzün arkasına takılır ve
    /// başka hiçbir yer değişmez.
    /// </remarks>
    public interface IPasswordResetNotifier
    {
        /// <summary>
        /// Teslimat gerçekten yapılabiliyor mu? <c>false</c> ise uç 503 dönüyor
        /// ve Web "şifremi unuttum" bağlantısını göstermiyor — kullanıcıya
        /// "bağlantı gönderildi" deyip hiçbir şey göndermemektense özelliğin
        /// kapalı olduğunu söylemek dürüst olan.
        /// </summary>
        bool IsEnabled { get; }

        Task SendAsync(string email, string resetUrl, CancellationToken cancellationToken = default);
    }
}
