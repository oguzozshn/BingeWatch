using BingeWatch.API.Configurations;

namespace BingeWatch.Tests
{
    /// <summary>
    /// <see cref="SmtpSettings.IsConfigured"/> bir özelliğin açık mı kapalı mı
    /// olduğuna karar veriyor: yanlış <c>true</c> dönerse uygulama gönderemeyeceği
    /// mailleri "gönderdim" sayar, yanlış <c>false</c> dönerse çalışan kurulumda
    /// özellik sessizce kapalı kalır.
    /// </summary>
    public class SmtpSettingsTests
    {
        [Fact]
        public void IsConfigured_IsFalse_WhenNothingIsSet()
        {
            Assert.False(new SmtpSettings().IsConfigured);
        }

        [Theory]
        [InlineData(null, "posta@ornek.test")]
        [InlineData("smtp.ornek.test", null)]
        [InlineData("", "posta@ornek.test")]
        [InlineData("smtp.ornek.test", "   ")]
        public void IsConfigured_IsFalse_WhenHostOrSenderIsMissing(string? host, string? from)
        {
            var settings = new SmtpSettings { Host = host, FromAddress = from };

            Assert.False(settings.IsConfigured);
        }

        [Fact]
        public void IsConfigured_IsTrue_WithoutCredentials()
        {
            // Kimlik doğrulaması istemeyen sunucular (yerel sahte SMTP, kurum içi
            // relay) gecerli bir kurulum; kullanıcı adı/parola şart koşulmamalı.
            var settings = new SmtpSettings
            {
                Host = "localhost",
                Port = 1025,
                FromAddress = "bingewatch@ornek.test"
            };

            Assert.True(settings.IsConfigured);
        }

        [Fact]
        public void Defaults_MatchStartTlsSubmissionPort()
        {
            // 587 + TLS, sağlayıcıların büyük çoğunluğunun beklediği kurulum;
            // yanlış varsayılan "neden bağlanamıyor" turuna çıkarıyor.
            var settings = new SmtpSettings();

            Assert.Equal(587, settings.Port);
            Assert.True(settings.UseTls);
        }
    }
}
