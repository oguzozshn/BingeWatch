using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace BingeWatch.E2E
{
    /// <summary>
    /// Giriş gerektiren akışlar. ROADMAP §7.1 bunları açıkça bekliyordu: dizi
    /// sayfasının sekme deseni ve moderasyon akışı giriş istediği için Faz 6.3'te
    /// yalnızca koda bakılarak doğrulanabilmişti.
    /// </summary>
    [Collection(AppCollection.Name)]
    public class AuthenticatedFlowTests
    {
        private const int SeedShowId = CatalogSeeder.ShowTmdbId;

        private readonly AppFixture _app;

        public AuthenticatedFlowTests(AppFixture app) => _app = app;

        /// <summary>
        /// Sezon akordiyonu varsayılan kapalı ama "ilk yarım kalmış sezon açık
        /// başlar" kuralı var; körlemesine tıklamak açığı kapatabiliyor.
        /// </summary>
        private static async Task ExpandFirstSeasonAsync(IPage page)
        {
            var season = page.Locator(".season-toggle").First;
            await Assertions.Expect(season).ToBeVisibleAsync();

            if (await season.GetAttributeAsync("aria-expanded") != "true")
                await season.ClickAsync();

            await Assertions.Expect(season).ToHaveAttributeAsync("aria-expanded", "true");
        }

        /// <summary>
        /// Dizi sayfası <c>InteractiveServer</c>: ilk gelen HTML prerender
        /// çıktısı ve SignalR devresi bağlanana kadar <b>ölü</b> — o aralıkta
        /// yapılan tıklama ve tuş vuruşları sessizce kayboluyor. Devrenin
        /// açtığı WebSocket'i beklemek testlerin gerçek hatayı mı yoksa yarışı
        /// mı gösterdiğini ayırt etmenin tek yolu.
        /// </summary>
        private async Task<IPage> OpenShowAsync()
        {
            var page = await _app.NewPageAsync(_app.PrimaryUser);

            // Abone olmak gezinmeden önce olmalı, yoksa soket kaçırılabilir.
            var console = new List<string>();
            page.Console += (_, message) => console.Add($"{message.Type}: {message.Text}");
            page.PageError += (_, error) => console.Add($"pageerror: {error}");

            var socket = page.WaitForWebSocketAsync();
            await page.GotoAsync($"{AppFixture.WebUrl}/show/{SeedShowId}");
            await socket;

            await WaitForInteractiveTabsAsync(page, console, _app);
            return page;
        }

        /// <summary>
        /// Soketin açılması el sıkışmanın bittiği anlamına gelmiyor. Sekme
        /// gerçekten tepki verene kadar tıklamayı yineler — sınırlı sayıda,
        /// gerçek bir kırıklığı sonsuza dek maskelemesin.
        /// </summary>
        private static async Task WaitForInteractiveTabsAsync(
            IPage page, List<string>? console = null, AppFixture? app = null)
        {
            await Assertions.Expect(page.Locator("[role=tablist]")).ToBeVisibleAsync();

            var episodes = page.GetByRole(AriaRole.Tab, new() { Name = "Bölümler" });
            var overview = page.GetByRole(AriaRole.Tab, new() { Name = "Genel Bakış" });

            for (var attempt = 0; attempt < 15; attempt++)
            {
                await episodes.ClickAsync();
                try
                {
                    await Assertions.Expect(episodes)
                        .ToHaveAttributeAsync("aria-selected", "true", new() { Timeout = 1_000 });

                    // Etkileşim çalışıyor; başlangıç sekmesine geri dön ki her
                    // test aynı noktadan başlasın.
                    await overview.ClickAsync();
                    await Assertions.Expect(overview).ToHaveAttributeAsync("aria-selected", "true");
                    return;
                }
                catch (PlaywrightException)
                {
                    // Devre henüz bağlanmadı.
                }
            }

            var errorUi = await page.Locator("#blazor-error-ui").IsVisibleAsync();
            var log = console == null || console.Count == 0
                ? "(konsol boş)"
                : string.Join(Environment.NewLine, console.TakeLast(15));

            throw new TimeoutException(
                $"Dizi sayfası etkileşimli hale gelmedi. Blazor hata çubuğu görünür mü: {errorUi}." +
                $"{Environment.NewLine}Konsol:{Environment.NewLine}{log}" +
                $"{Environment.NewLine}Web sunucu logu:{Environment.NewLine}{app?.WebLogTail()}");
        }

        [Fact]
        public async Task PersonalLayer_IsVisibleWhenSignedIn()
        {
            var page = await OpenShowAsync();

            // Anonimde gizlenen katman girişte görünmeli — PublicPageTests'in
            // aynadaki karşılığı.
            await Assertions.Expect(page.Locator(".signin-nudge")).ToHaveCountAsync(0);
            await Assertions.Expect(
                page.GetByRole(AriaRole.Tab, new() { Name = "Bölümler" })).ToBeVisibleAsync();
        }

        /// <summary>
        /// Faz 6.3 sekmeleri tam ARIA desenine geçirdi: şeritte tek odak durağı,
        /// ok tuşlarıyla geçiş, Home/End uçlara. Klavyeyle hiç denenmemişti.
        /// </summary>
        [Fact]
        public async Task ShowTabs_FollowAriaKeyboardPattern()
        {
            var page = await OpenShowAsync();

            var tabs = page.GetByRole(AriaRole.Tab);
            var count = await tabs.CountAsync();
            Assert.Equal(4, count);

            // Şeritte tek durak: yalnızca seçili sekme tabindex=0 olmalı.
            var tabbable = await page.Locator("[role=tab][tabindex='0']").CountAsync();
            Assert.Equal(1, tabbable);

            var first = tabs.Nth(0);
            await first.FocusAsync();
            await Assertions.Expect(first).ToHaveAttributeAsync("aria-selected", "true");

            // Sağ ok bir sonraki sekmeye geçmeli ve onu seçmeli.
            await page.Keyboard.PressAsync("ArrowRight");
            await Assertions.Expect(tabs.Nth(1)).ToBeFocusedAsync();
            await Assertions.Expect(tabs.Nth(1)).ToHaveAttributeAsync("aria-selected", "true");
            await Assertions.Expect(tabs.Nth(0)).ToHaveAttributeAsync("aria-selected", "false");

            // End son sekmeye, Home başa.
            await page.Keyboard.PressAsync("End");
            await Assertions.Expect(tabs.Nth(count - 1)).ToBeFocusedAsync();

            await page.Keyboard.PressAsync("Home");
            await Assertions.Expect(tabs.Nth(0)).ToBeFocusedAsync();

            // Panel, seçili sekmeye bağlı olmalı.
            var panelLabelledBy = await page.Locator("[role=tabpanel]").GetAttributeAsync("aria-labelledby");
            var selectedId = await page.Locator("[role=tab][aria-selected=true]").GetAttributeAsync("id");
            Assert.Equal(selectedId, panelLabelledBy);
        }

        /// <summary>
        /// Ürünün belkemiği: bölüm işaretleme ilerlemeyi ve durumu güncellemeli,
        /// yenilemeden sonra da kalmalı.
        /// </summary>
        [Fact]
        public async Task MarkingEpisode_PersistsAcrossReload()
        {
            var page = await OpenShowAsync();

            await page.GetByRole(AriaRole.Tab, new() { Name = "Bölümler" }).ClickAsync();

            await ExpandFirstSeasonAsync(page);

            var firstCheckbox = page.Locator(".episode-checkbox input[type=checkbox]").First;
            await Assertions.Expect(firstCheckbox).ToBeVisibleAsync();

            var wasChecked = await firstCheckbox.IsCheckedAsync();
            await firstCheckbox.SetCheckedAsync(!wasChecked);

            // İşaretleme API'ye gidiyor; satırın "watched" sınıfı sunucudan
            // dönen cevapla geliyor. Beklemeden yenilersek isteği yarıda kesip
            // "kalıcı olmadı" diye yanlış rapor ederiz.
            var firstRow = page.Locator(".episode-row").First;
            if (wasChecked)
                await Assertions.Expect(firstRow).Not.ToHaveClassAsync(new Regex(@"\bwatched\b"));
            else
                await Assertions.Expect(firstRow).ToHaveClassAsync(new Regex(@"\bwatched\b"));

            // Yeniden yükle: işaret sunucuda kalmalı, yalnızca ekranda değil.
            await page.ReloadAsync();
            // Yeniden yükleme devreyi de sıfırlıyor; tekrar beklenmeli.
            await WaitForInteractiveTabsAsync(page);
            await page.GetByRole(AriaRole.Tab, new() { Name = "Bölümler" }).ClickAsync();

            await ExpandFirstSeasonAsync(page);

            var afterReload = page.Locator(".episode-checkbox input[type=checkbox]").First;
            await Assertions.Expect(afterReload).ToBeVisibleAsync();
            Assert.Equal(!wasChecked, await afterReload.IsCheckedAsync());
        }

        /// <summary>
        /// Engelleme tek yönlü kaydedilir, iki yönlü etki eder ve hangi yönde
        /// olduğu sızmasın diye iki tarafa da 404 döner (Faz 6.1). Bu davranış
        /// gerçek oturumda hiç denenmemişti.
        /// </summary>
        [Fact]
        public async Task BlockedUser_ProfileIsHiddenBothWays()
        {
            var blocker = await _app.NewPageAsync(_app.PrimaryUser);
            var blocked = _app.SecondaryUser;

            // Engellemeden önce profil görünüyor olmalı — testin kendisi
            // anlamlı olsun diye başlangıç durumu doğrulanıyor.
            var before = await blocker.GotoAsync($"{AppFixture.WebUrl}/@{blocked.Username}");
            Assert.Equal(200, before!.Status);

            // Engelleme tek tıkla değil onay adımıyla: sert ve yarı kalıcı bir
            // eylem (engeli kaldırmak takipleri geri getirmiyor).
            await blocker.GetByRole(AriaRole.Button, new() { Name = "Engelle", Exact = true })
                .ClickAsync();
            await Assertions.Expect(blocker.Locator(".block-confirm")).ToBeVisibleAsync();
            await blocker.Locator(".block-confirm button.btn-danger").ClickAsync();

            // Onaydan sonra engel listesine yönlendiriyor — karşı profil artık
            // 404 döndüğü için kullanıcı ne olduğunu görebilsin diye.
            await blocker.WaitForURLAsync("**/settings/blocks", new() { Timeout = 15_000 });
            await Assertions.Expect(blocker.GetByText(blocked.Username)).ToBeVisibleAsync(
                new() { Timeout = 10_000 });

            // Engelleyen taraf artık profili görememeli.
            await blocker.GotoAsync($"{AppFixture.WebUrl}/@{blocked.Username}");
            await Assertions.Expect(blocker.GetByText("Kullanıcı bulunamadı")).ToBeVisibleAsync(
                new() { Timeout = 10_000 });

            // Engellenen taraf da karşıyı görememeli — ve hangi yönde
            // engellendiğini anlamamalı: aynı "bulunamadı".
            var other = await _app.NewPageAsync(blocked);
            await other.GotoAsync($"{AppFixture.WebUrl}/@{_app.PrimaryUser.Username}");
            await Assertions.Expect(other.GetByText("Kullanıcı bulunamadı")).ToBeVisibleAsync(
                new() { Timeout = 10_000 });
        }
    }
}
