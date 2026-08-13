using Microsoft.Playwright;

namespace BingeWatch.E2E
{
    /// <summary>
    /// Web katmanının sessiz kırılmaları. Üçü de gerçekten yaşandı ve hiçbiri
    /// mevcut testlerden birine takılmadı — çünkü hepsi "sayfa geliyor, öğe
    /// var" seviyesinde sağlıklı görünüyordu:
    ///
    /// <list type="bullet">
    /// <item>İkon fontu hiç bağlanmamıştı; ikonlar boş kutu olarak çiziliyordu.</item>
    /// <item>Linux'ta kültür ayarlanmadığı için tarihler İngilizce geliyordu.</item>
    /// <item>Prerender edilmiş butonlar devre kurulmadan tıklanabilir görünüyordu.</item>
    /// </list>
    ///
    /// Öğenin varlığını değil, kullanıcının gördüğü sonucu ölçüyorlar.
    /// </summary>
    [Collection(AppCollection.Name)]
    public class RenderingRegressionTests
    {
        private const int SeedShowId = CatalogSeeder.ShowTmdbId;

        private readonly AppFixture _app;

        public RenderingRegressionTests(AppFixture app) => _app = app;

        /// <summary>
        /// Sayfanın çalışması için gereken varlıklar. <c>blazor.web.js</c> 404
        /// olursa site çalışıyor *görünür* ama hiçbir şey tıklanmaz; ikon CSS'i
        /// eksik olursa ikonlar sessizce kaybolur.
        /// </summary>
        [Theory]
        [InlineData("/lib/bootstrap/dist/css/bootstrap.min.css")]
        [InlineData("/lib/bootstrap-icons/font/bootstrap-icons.min.css")]
        [InlineData("/lib/bootstrap-icons/font/fonts/bootstrap-icons.woff2")]
        [InlineData("/_framework/blazor.web.js")]
        public async Task CriticalAssets_AreServed(string path)
        {
            var page = await _app.NewPageAsync();
            await page.GotoAsync(AppFixture.WebUrl);

            // GotoAsync yerine fetch: font dosyasına gezinmek tarayıcıda
            // indirme başlatıyor ve gezinme hiç tamamlanmıyor.
            var status = await page.EvaluateAsync<int>(
                $"async () => (await fetch('{path}')).status");

            Assert.True(status == 200, $"{path} → {status}");
        }

        /// <summary>
        /// Varlığın sunulması yetmiyor: font gerçekten yüklenmiş ve ikonlar
        /// yer kaplıyor olmalı. Eksik CSS'te <c>&lt;i&gt;</c> etiketleri
        /// DOM'da duruyordu — yalnızca genişlikleri sıfırdı.
        /// </summary>
        [Fact]
        public async Task IconFont_IsLoadedAndIconsRender()
        {
            var page = await _app.NewPageAsync();
            await page.GotoAsync($"{AppFixture.WebUrl}/show/{SeedShowId}");

            var fontLoaded = await page.EvaluateAsync<bool>(
                "() => document.fonts.check('16px bootstrap-icons')");
            Assert.True(fontLoaded, "bootstrap-icons fontu yüklenmedi");

            var iconsHaveSize = await page.EvaluateAsync<bool>(@"() => {
                const icons = [...document.querySelectorAll('[class*=""bi-""]')];
                return icons.length > 0 && icons.every(i => i.getBoundingClientRect().width > 0);
            }");
            Assert.True(iconsHaveSize, "ikonlar çizilmiyor (genişlik 0)");
        }

        /// <summary>
        /// Arayüzün tamamı Türkçe; tarih ve sayı biçimi işletim sisteminin
        /// kültürüne bırakılamaz. Windows'ta gizli kalan, Linux'ta ortaya
        /// çıkan bir hataydı — CI Linux'ta koştuğu için buradan yakalanır.
        /// </summary>
        /// <remarks>
        /// ⚠️ Bu test ve kardeşi (<see cref="Numbers_UseTurkishDecimalSeparator"/>)
        /// bir süre CI'da dönüşümlü olarak düştü: <c>GotoAsync</c>'ten hemen sonra
        /// <c>InnerTextAsync</c> ile <b>anlık görüntü</b> alıyorlardı. Prerender
        /// edilmiş sayfa devre devralırken bileşen yeniden kuruluyor ve o kısa
        /// aralıkta "Yükleniyor..." gösteriyor; anlık görüntü oraya denk gelince
        /// test kültür hatası varmış gibi rapor veriyordu. Yerelde hiç
        /// görünmüyordu, Linux koşucuda aralık yetecek kadar genişti.
        /// <para>
        /// Çözüm beklemeyi Playwright'a bırakmak: <c>ToContainTextAsync</c>
        /// içerik gelene kadar yeniden deniyor. Aynı hata kalıbı innerText
        /// anlık görüntüsü alan her testte var.
        /// </para>
        /// </remarks>
        [Fact]
        public async Task Dates_AreFormattedInTurkish()
        {
            var page = await _app.NewPageAsync();
            await page.GotoAsync($"{AppFixture.WebUrl}/show/{SeedShowId}/season/1/episode/1");

            // Tohumlanan yayın tarihi: 20 Ocak 2008.
            await Assertions.Expect(page.Locator("body")).ToContainTextAsync("Ocak");
            Assert.DoesNotContain("January", await page.Locator("body").InnerTextAsync());
        }

        /// <summary>Ondalık ayırıcı da kültüre bağlı: 7,8 — 7.8 değil.</summary>
        [Fact]
        public async Task Numbers_UseTurkishDecimalSeparator()
        {
            var page = await _app.NewPageAsync();
            await page.GotoAsync($"{AppFixture.WebUrl}/show/{SeedShowId}/season/1/episode/1");

            // Tohumlanan bölüm puanı 7,8 (7.5 + 1 * 0.3).
            await Assertions.Expect(page.Locator("body")).ToContainTextAsync("7,8");
        }

        /// <summary>
        /// Anonim ziyaretçiye kişisel sayfaların linki gösterilmemeli. Erişim
        /// zaten <c>[Authorize]</c> ile kapalı — ama linki göstermek var
        /// olmayan bir kapı göstermek: tıklayan kullanıcı hiçbir açıklama
        /// görmeden giriş ekranına düşüyordu.
        /// </summary>
        [Theory]
        [InlineData("/watchlist")]
        [InlineData("/feed")]
        [InlineData("/notifications")]
        [InlineData("/settings/profile")]
        public async Task Navbar_HidesPersonalLinksFromAnonymousVisitor(string path)
        {
            var page = await _app.NewPageAsync();
            await page.GotoAsync($"{AppFixture.WebUrl}/login");

            var linkCount = await page.Locator($"nav a[href='{path.TrimStart('/')}']").CountAsync();

            Assert.Equal(0, linkCount);
        }

        /// <summary>
        /// Linkin gizlenmesi yetmez: adres elle yazıldığında da erişim
        /// kapalı olmalı.
        /// </summary>
        [Theory]
        [InlineData("/watchlist")]
        [InlineData("/feed")]
        [InlineData("/notifications")]
        [InlineData("/settings/profile")]
        [InlineData("/settings/blocks")]
        public async Task PersonalPages_RedirectAnonymousToLogin(string path)
        {
            var page = await _app.NewPageAsync();

            var response = await page.GotoAsync($"{AppFixture.WebUrl}{path}");

            Assert.NotNull(response);
            Assert.Contains("/login", page.Url);
        }

        /// <summary>
        /// Prerender edilmiş HTML, devre bağlanana kadar ölü. Buton o aralıkta
        /// tıklanabilir görünürse kullanıcı basar, hiçbir şey olmaz ve nedenini
        /// anlamaz. Sunucudan gelen ham HTML'e bakıyoruz: tarayıcı devreyi
        /// kurduktan sonra buton zaten etkinleşiyor.
        /// </summary>
        [Fact]
        public async Task InteractiveButtons_AreDisabledInPrerenderedHtml()
        {
            // Buton yalnızca girişli kullanıcıya çiziliyor.
            var page = await _app.NewPageAsync(_app.PrimaryUser);
            await page.GotoAsync($"{AppFixture.WebUrl}/show/{SeedShowId}");

            var html = await page.EvaluateAsync<string>($@"async () => {{
                const r = await fetch('{AppFixture.WebUrl}/show/{SeedShowId}/season/1/episode/1',
                    {{ headers: {{ 'Accept': 'text/html' }} }});
                return await r.text();
            }}");

            var marker = "class=\"watch-btn";
            var index = html.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(index >= 0, "prerender edilmiş HTML'de izleme butonu yok");

            // Butonun kendi etiketi içinde disabled aranıyor; etiket sonuna
            // kadar olan dilim yeterli.
            var tagEnd = html.IndexOf('>', index);
            var tag = html[index..tagEnd];
            Assert.Contains("disabled", tag);
        }
    }
}
