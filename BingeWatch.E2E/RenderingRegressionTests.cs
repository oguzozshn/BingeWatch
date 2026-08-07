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
        [Fact]
        public async Task Dates_AreFormattedInTurkish()
        {
            var page = await _app.NewPageAsync();
            await page.GotoAsync($"{AppFixture.WebUrl}/show/{SeedShowId}/season/1/episode/1");

            var body = await page.Locator("body").InnerTextAsync();

            // Tohumlanan yayın tarihi: 20 Ocak 2008.
            Assert.Contains("Ocak", body);
            Assert.DoesNotContain("January", body);
        }

        /// <summary>Ondalık ayırıcı da kültüre bağlı: 7,8 — 7.8 değil.</summary>
        [Fact]
        public async Task Numbers_UseTurkishDecimalSeparator()
        {
            var page = await _app.NewPageAsync();
            await page.GotoAsync($"{AppFixture.WebUrl}/show/{SeedShowId}/season/1/episode/1");

            var body = await page.Locator("body").InnerTextAsync();

            // Tohumlanan bölüm puanı 7,8 (7.5 + 1 * 0.3).
            Assert.Contains("7,8", body);
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
