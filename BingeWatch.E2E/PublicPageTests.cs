using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Playwright;

namespace BingeWatch.E2E
{
    /// <summary>
    /// Giriş gerektirmeyen yollar: anonim ziyaretçinin ve arama motorunun gördüğü
    /// yüzey. Faz 6.4'te dizi sayfası herkese açıldı — bu testler o kararın geri
    /// alınmadığının bekçisi.
    /// </summary>
    [Collection(AppCollection.Name)]
    public class PublicPageTests
    {
        /// <summary>Katalogda kesin bulunan bir dizi — fixture tohumluyor.</summary>
        private const int SeedShowId = CatalogSeeder.ShowTmdbId;

        private readonly AppFixture _app;

        public PublicPageTests(AppFixture app) => _app = app;

        [Fact]
        public async Task ShowPage_IsReachableWithoutLogin()
        {
            var page = await _app.NewPageAsync();

            var response = await page.GotoAsync($"{AppFixture.WebUrl}/show/{SeedShowId}");

            Assert.Equal(200, response!.Status);
            // Giriş sayfasına yönlenmemeli.
            Assert.Contains($"/show/{SeedShowId}", page.Url);

            await Assertions.Expect(page.Locator("h1")).ToBeVisibleAsync();
            Assert.False(string.IsNullOrWhiteSpace(await page.Locator("h1").First.TextContentAsync()));
        }

        [Fact]
        public async Task ShowPage_HidesPersonalLayerFromAnonymousVisitor()
        {
            var page = await _app.NewPageAsync();
            await page.GotoAsync($"{AppFixture.WebUrl}/show/{SeedShowId}");

            // Kişisel katman <AuthorizeView> arkasında; anonimde hiç çizilmemeli.
            await Assertions.Expect(page.GetByText("Puanın")).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByText("İnceleme yaz")).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByText("Sezonu izledim")).ToHaveCountAsync(0);

            // Ama giriş çağrısı görünmeli.
            await Assertions.Expect(page.Locator(".signin-nudge")).ToBeVisibleAsync();
        }

        [Fact]
        public async Task ShowPage_EmitsSocialAndStructuredMetadata()
        {
            var page = await _app.NewPageAsync();
            await page.GotoAsync($"{AppFixture.WebUrl}/show/{SeedShowId}");

            var ogTitle = await page.Locator("meta[property='og:title']").GetAttributeAsync("content");
            var ogType = await page.Locator("meta[property='og:type']").GetAttributeAsync("content");
            var canonical = await page.Locator("link[rel='canonical']").GetAttributeAsync("href");

            Assert.False(string.IsNullOrWhiteSpace(ogTitle));
            Assert.Equal("video.tv_show", ogType);
            Assert.Contains($"/show/{SeedShowId}", canonical);
            // Canonical sorgu dizesi taşımamalı.
            Assert.DoesNotContain("?", canonical);

            var jsonLd = await page.Locator("script[type='application/ld+json']").TextContentAsync();
            using var document = JsonDocument.Parse(jsonLd!);

            Assert.Equal("TVSeries", document.RootElement.GetProperty("@type").GetString());
            // Katalog tohumlandığı için kesin sayı beklenebiliyor.
            Assert.Equal(CatalogSeeder.SeasonCount,
                document.RootElement.GetProperty("numberOfSeasons").GetInt32());
        }

        /// <summary>
        /// Kişisel sayfalar anonim ziyaretçiye — ve dolayısıyla arama motoruna —
        /// hiç çizilmemeli.
        /// <para>
        /// Bu test önce sayfalarda <c>noindex</c> meta etiketi arıyordu ve
        /// başarısız oluyordu. Sebebi ilginç: sayfalar zaten HTTP katmanında
        /// 302 ile <c>/login</c>'e gidiyor, yani gövde hiç üretilmiyor. Meta
        /// etiketi doğru davranışın kanıtı değil — yönlendirme o.
        /// </para>
        /// </summary>
        [Theory]
        [InlineData("/notifications")]
        [InlineData("/feed")]
        [InlineData("/watchlist")]
        [InlineData("/settings/blocks")]
        [InlineData("/admin/reports")]
        public async Task PrivatePages_RedirectAnonymousVisitorToLogin(string path)
        {
            // Yönlendirmenin kendisini görmek istiyoruz, hedefini değil.
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var client = new HttpClient(handler);

            var response = await client.GetAsync($"{AppFixture.WebUrl}{path}");

            Assert.Equal(HttpStatusCode.Found, response.StatusCode);
            Assert.Contains("/login", response.Headers.Location!.ToString());
            // Gövde sızmamalı: 302 ile birlikte içerik dönerse anonim ziyaretçi
            // yine de okuyabilir.
            Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        }

        [Fact]
        public async Task Robots_DisallowsPrivateAreasAndPointsToSitemap()
        {
            using var client = new HttpClient();

            var robots = await client.GetStringAsync($"{AppFixture.WebUrl}/robots.txt");

            Assert.Contains("Disallow: /admin/", robots);
            Assert.Contains("Disallow: /settings/", robots);
            Assert.Contains("sitemap.xml", robots);
        }

        [Fact]
        public async Task Sitemap_IsValidXmlAndListsShows()
        {
            using var client = new HttpClient();

            var xml = await client.GetStringAsync($"{AppFixture.WebUrl}/sitemap.xml");
            var document = XDocument.Parse(xml);

            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            var locations = document.Root!.Elements(ns + "url")
                .Select(url => url.Element(ns + "loc")!.Value)
                .ToList();

            Assert.Contains(locations, url => url.EndsWith("/series"));
            Assert.Contains(locations, url => url.Contains("/show/"));
            // Kişisel sayfalar sitemap'e girmemeli.
            Assert.DoesNotContain(locations, url => url.Contains("/notifications"));
        }

        [Fact]
        public async Task Pages_DeclareTurkishLanguage()
        {
            // lang="en" iken ekran okuyucu Türkçe metni İngilizce fonemlerle
            // okuyordu (Faz 6.3); geri gelmesin.
            var page = await _app.NewPageAsync();
            await page.GotoAsync($"{AppFixture.WebUrl}/reviews");

            Assert.Equal("tr", await page.Locator("html").GetAttributeAsync("lang"));
        }

        [Fact]
        public async Task SkipLink_IsFirstStopAndBecomesVisibleOnFocus()
        {
            var page = await _app.NewPageAsync();
            await page.GotoAsync($"{AppFixture.WebUrl}/reviews");

            const string Describe =
                "() => { const a = document.activeElement; return a " +
                "? a.tagName.toLowerCase() + (a.className ? '.' + String(a.className).trim().split(/\\s+/).join('.') : '') " +
                ": 'null'; }";

            var onLoad = await page.EvaluateAsync<string>(Describe);
            await page.Keyboard.PressAsync("Tab");
            var afterTab = await page.EvaluateAsync<string>(Describe);

            Assert.True(afterTab.Contains("skip-link"),
                $"İlk Tab atlama bağlantısına gitmeliydi. Yüklemede odak: '{onLoad}', Tab sonrası: '{afterTab}'.");

            // Odaklanınca ekran dışından içeri kaymalı.
            await Assertions.Expect(page.Locator(".skip-link")).ToBeInViewportAsync();
        }

        /// <summary>
        /// Odak yönetiminin diğer yarısı: ilk yüklemede odağa dokunulmuyor ama
        /// gezinmeden sonra ekran okuyucunun yeni sayfayı duyurabilmesi için
        /// odak başlığa taşınmalı. İkisi birlikte doğru olmazsa düzeltme
        /// tek yönlü kalır — bu yüzden ayrı test.
        /// </summary>
        [Fact]
        public async Task Navigation_MovesFocusToHeadingOfNewPage()
        {
            var page = await _app.NewPageAsync();
            await page.GotoAsync($"{AppFixture.WebUrl}/reviews");

            // Gerçek bir gezinme: enhanced navigation'ı tetiklemesi için
            // bağlantıya tıklanıyor, GotoAsync ile tam yükleme yapılmıyor.
            await page.Locator("a.skip-link").WaitForAsync();
            // /lists anonime açık ve TMDb'ye gitmiyor; gezinme testini dış
            // servise bağlamayalım.
            await page.GetByRole(AriaRole.Link, new() { Name = "Listeler", Exact = false }).First.ClickAsync();
            await page.WaitForURLAsync("**/lists");

            // Odaklama 'enhancedload' sonrasına ve birkaç denemeye yayılıyor;
            // anlık okuma yarışa giriyor, bu yüzden yoklayan assertion.
            await Assertions.Expect(page.Locator("h1")).ToBeFocusedAsync(new() { Timeout = 10_000 });
        }
    }
}
