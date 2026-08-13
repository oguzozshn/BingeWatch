using Microsoft.Playwright;

namespace BingeWatch.E2E
{
    /// <summary>
    /// Şifre değiştirme akışı (Faz 9.1).
    /// <para>
    /// Her iki test de kendi hesabını açıyor: parola değiştiren bir test
    /// paylaşılan hesapları kullanan diğer testleri bozardı — üstelik damga
    /// yenilendiği için o hesabın enjekte edilen çerezi de geçersizleşirdi.
    /// </para>
    /// </summary>
    [Collection(AppCollection.Name)]
    public class ChangePasswordTests
    {
        private const string NewPassword = "YepyeniSifre!2026";

        private readonly AppFixture _app;

        public ChangePasswordTests(AppFixture app) => _app = app;

        [Fact]
        public async Task ChangePassword_RejectsWrongCurrentPassword()
        {
            var user = await _app.RegisterUserAsync("sifredegis");
            var page = await _app.NewPageAsync(user);

            await page.GotoAsync($"{AppFixture.WebUrl}/settings/password");
            await FillFormAsync(page, current: "TamamenBaska!9", @new: NewPassword, confirm: NewPassword);

            // Mevcut şifre kapısı sunucuda; mesaj da parola kuralı hatasından ayrı.
            await Assertions.Expect(page.GetByText("Mevcut şifren doğru değil"))
                .ToBeVisibleAsync(new() { Timeout = 15_000 });
        }

        [Fact]
        public async Task ChangePassword_MismatchedConfirmationNeverReachesApi()
        {
            var user = await _app.RegisterUserAsync("sifredegis");
            var page = await _app.NewPageAsync(user);

            await page.GotoAsync($"{AppFixture.WebUrl}/settings/password");
            await FillFormAsync(page, current: AppFixture.Password, @new: NewPassword,
                confirm: NewPassword + "-farkli");

            await Assertions.Expect(page.GetByText("Yeni şifreler birbirini tutmuyor"))
                .ToBeVisibleAsync(new() { Timeout = 15_000 });

            // Eski parola hâlâ geçerli olmalı: istek API'ye hiç gitmedi.
            await AssertLoginAsync(page, user.Username, AppFixture.Password);
        }

        [Fact]
        public async Task ChangePassword_KeepsOwnSessionAndNewPasswordWorks()
        {
            var user = await _app.RegisterUserAsync("sifredegis");
            var page = await _app.NewPageAsync(user);

            await page.GotoAsync($"{AppFixture.WebUrl}/settings/password");
            await FillFormAsync(page, current: AppFixture.Password, @new: NewPassword, confirm: NewPassword);

            await Assertions.Expect(page.GetByText("Şifren güncellendi"))
                .ToBeVisibleAsync(new() { Timeout = 15_000 });

            // Kendi oturumu düşmedi. Damga yenilendiği için cookie'deki eski
            // token artık 401 alırdı; bu sayfanın açılabilmesi cookie'nin taze
            // token'la yeniden yazıldığının kanıtı (form API'den dolduruluyor).
            await page.GotoAsync($"{AppFixture.WebUrl}/settings/profile");
            await Assertions.Expect(page.Locator("#display-name"))
                .ToBeVisibleAsync(new() { Timeout = 20_000 });

            // Ve değişiklik gerçekten yazıldı: yeni parolayla giriş yapılıyor.
            await AssertLoginAsync(page, user.Username, NewPassword);
        }

        private static async Task FillFormAsync(IPage page, string current, string @new, string confirm)
        {
            await Assertions.Expect(page.Locator("#current-password")).ToBeVisibleAsync();

            await page.FillAsync("#current-password", current);
            await page.FillAsync("#new-password", @new);
            await page.FillAsync("#confirm-password", confirm);
            await page.ClickAsync("form[action='/account/change-password'] button[type=submit]");
        }

        /// <summary>Verilen parolayla giriş yapılabildiğini doğrular.</summary>
        private static async Task AssertLoginAsync(IPage page, string username, string password)
        {
            await page.GotoAsync($"{AppFixture.WebUrl}/login");
            await page.FillAsync("#login-username", username);
            await page.FillAsync("#login-password", password);
            await page.ClickAsync("form[action='/account/login'] button[type=submit]");

            await page.WaitForURLAsync(url => !url.Contains("/login"),
                new PageWaitForURLOptions { Timeout = 20_000 });
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = username }))
                .ToBeVisibleAsync(new() { Timeout = 10_000 });
        }
    }
}
