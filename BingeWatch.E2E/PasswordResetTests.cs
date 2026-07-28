using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace BingeWatch.E2E
{
    /// <summary>
    /// Şifre sıfırlama akışı — Faz 1'de ertelenmişti.
    /// <para>
    /// E-posta altyapısı yok; Development'ta bağlantı loga yazılıyor
    /// (<c>LoggingPasswordResetNotifier</c>). Test bağlantıyı API'nin
    /// çıktısından okuyor, yani akışın tamamı — token üretimi, bağlantının
    /// biçimi, sıfırlama ve yeni parolayla giriş — gerçekten sınanıyor.
    /// </para>
    /// </summary>
    [Collection(AppCollection.Name)]
    public class PasswordResetTests
    {
        private readonly AppFixture _app;

        public PasswordResetTests(AppFixture app) => _app = app;

        [Fact]
        public async Task ForgotPassword_DoesNotRevealWhetherAccountExists()
        {
            var page = await _app.NewPageAsync();
            await page.GotoAsync($"{AppFixture.WebUrl}/forgot-password");

            // Kesinlikle kayıtlı olmayan bir adres.
            await page.FillAsync("#forgot-email", $"yok-{Guid.NewGuid():N}@ornek.test");
            await page.ClickAsync("button[type=submit]");

            // Yanıt, hesap varmış gibi. "Böyle bir kullanıcı yok" demek,
            // adresleri tek tek deneyerek üyeleri saymaya izin verirdi.
            await Assertions.Expect(page.GetByText("sıfırlama bağlantısı gönderildi"))
                .ToBeVisibleAsync();
        }

        [Fact]
        public async Task ResetPassword_CompletesAndNewPasswordWorks()
        {
            // Bu test parolayı değiştiriyor; paylaşılan hesapları bozmasın diye
            // kendi kullanıcısını açıyor.
            var user = await _app.RegisterUserAsync("sifirla");
            const string NewPassword = "YeniSifre!2026";

            var page = await _app.NewPageAsync();
            await page.GotoAsync($"{AppFixture.WebUrl}/forgot-password");
            await page.FillAsync("#forgot-email", user.Email);
            await page.ClickAsync("button[type=submit]");
            await Assertions.Expect(page.GetByText("sıfırlama bağlantısı gönderildi"))
                .ToBeVisibleAsync();

            var resetUrl = ExtractResetUrl(_app.ApiLogTail(120), user.Email);

            await page.GotoAsync(resetUrl);
            await Assertions.Expect(page.Locator("#reset-password")).ToBeVisibleAsync();

            await page.FillAsync("#reset-password", NewPassword);
            await page.ClickAsync("button[type=submit]");

            // Sıfırlama sonrası giriş sayfasına dönüp onay göstermeli.
            await page.WaitForURLAsync("**/login**");
            await Assertions.Expect(page.GetByText("Şifren güncellendi")).ToBeVisibleAsync();

            // Asıl kanıt: yeni parolayla giriş çalışıyor.
            await page.FillAsync("#login-username", user.Username);
            await page.FillAsync("#login-password", NewPassword);
            await page.ClickAsync("form[action='/account/login'] button[type=submit]");

            await page.WaitForURLAsync(url => !url.Contains("/login"),
                new PageWaitForURLOptions { Timeout = 20_000 });
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = user.Username }))
                .ToBeVisibleAsync(new() { Timeout = 10_000 });
        }

        [Fact]
        public async Task ResetPassword_RejectsTamperedToken()
        {
            var page = await _app.NewPageAsync();

            await page.GotoAsync(
                $"{AppFixture.WebUrl}/reset-password?email=biri@ornek.test&token=kurcalanmis");
            await page.FillAsync("#reset-password", "BaskaSifre!2026");
            await page.ClickAsync("button[type=submit]");

            await Assertions.Expect(page.GetByText("geçersiz")).ToBeVisibleAsync(
                new() { Timeout = 10_000 });
        }

        /// <summary>
        /// Sıfırlama bağlantısını API logundan çeker. Log satırı
        /// <c>LoggingPasswordResetNotifier</c> tarafından yazılıyor.
        /// </summary>
        private static string ExtractResetUrl(string apiLog, string email)
        {
            // Aynı koşuda birden çok bağlantı olabilir; bu adrese ait olanı ve
            // en sonuncusunu al.
            var matches = Regex.Matches(apiLog, @"https?://\S*/reset-password\?\S+")
                .Select(m => m.Value.TrimEnd('.', ',', '"'))
                .Where(url => url.Contains(Uri.EscapeDataString(email), StringComparison.OrdinalIgnoreCase)
                              || url.Contains(email, StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.True(matches.Count > 0,
                $"API logunda {email} icin sifirlama baglantisi bulunamadi. Log:{Environment.NewLine}{apiLog}");

            return matches[^1];
        }
    }
}
