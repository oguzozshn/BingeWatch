using System.Text.Json;
using Microsoft.Playwright;

namespace BingeWatch.E2E
{
    /// <summary>
    /// Profildeki kütüphane sayfası (Faz 9.5) — başkasının izledikleri ve
    /// izleyecekleri.
    /// <para>
    /// Kütüphane durumu API üzerinden kuruluyor, arayüzü sürerek değil: test
    /// edilen şey dizi sayfasının işaretleme akışı değil (onun kendi testi var),
    /// kütüphanenin doğru okunup doğru sekmeye düşmesi. Her test kendi hesabını
    /// açıyor; paylaşılan hesapların kütüphanesini başka testler değiştiriyor.
    /// </para>
    /// </summary>
    [Collection(AppCollection.Name)]
    public class LibraryPageTests
    {
        private readonly AppFixture _app;

        public LibraryPageTests(AppFixture app) => _app = app;

        [Fact]
        public async Task Library_PutsPlannedShowInItsOwnTab()
        {
            var user = await _app.RegisterUserAsync("kutuphane");
            await AddToWatchlistAsync(user);

            var page = await _app.NewPageAsync(user);
            await page.GotoAsync($"{AppFixture.WebUrl}/@{user.Username}/library");

            // Varsayılan sekme "izledikleri" ve orası boş: dizi yalnızca
            // listeye eklendi, izlenmeye başlanmadı.
            var watched = page.GetByRole(AriaRole.Tab, new() { Name = "İzledikleri" });
            await Assertions.Expect(watched).ToHaveAttributeAsync("aria-selected", "true");
            await Assertions.Expect(page.GetByText("Henüz izlemeye başladığı bir dizi yok"))
                .ToBeVisibleAsync(new() { Timeout = 15_000 });

            // Dizi "izleyecekleri" sekmesinde.
            await page.GetByRole(AriaRole.Tab, new() { Name = "İzleyecekleri" }).ClickAsync();
            await Assertions.Expect(page.Locator($".show-card[href='/show/{CatalogSeeder.ShowTmdbId}']"))
                .ToBeVisibleAsync(new() { Timeout = 15_000 });
        }

        [Fact]
        public async Task Library_IsVisibleToOtherUsersButHiddenWhenProfileIsPrivate()
        {
            var owner = await _app.RegisterUserAsync("kutuphanesahip");
            await AddToWatchlistAsync(owner);

            // Başkası bakıyor: kütüphane görünür olmalı — ürünün kararı bu.
            var visitor = await _app.NewPageAsync(_app.PrimaryUser);
            await visitor.GotoAsync($"{AppFixture.WebUrl}/@{owner.Username}/library");
            await visitor.GetByRole(AriaRole.Tab, new() { Name = "İzleyecekleri" }).ClickAsync();
            await Assertions.Expect(visitor.Locator($".show-card[href='/show/{CatalogSeeder.ShowTmdbId}']"))
                .ToBeVisibleAsync(new() { Timeout = 15_000 });

            // Sahibi profilini gizleyince aynı sayfa kapanıyor. Yeni bir
            // gizlilik anahtarı yok; mevcut IsPrivate bu uçta da geçerli.
            await SetPrivateAsync(owner, isPrivate: true);

            await visitor.ReloadAsync();
            await Assertions.Expect(visitor.GetByText("Kullanıcı bulunamadı"))
                .ToBeVisibleAsync(new() { Timeout = 15_000 });
        }

        /// <summary>
        /// Hesabın token'ını alır. Çerez enjeksiyonu tarayıcı için yeterli ama
        /// API'ye doğrudan istek atmak Bearer token istiyor.
        /// </summary>
        private static async Task<IAPIRequestContext> ApiAsync(IPlaywright playwright, TestUser user)
        {
            var anonymous = await playwright.APIRequest.NewContextAsync(new()
            {
                BaseURL = AppFixture.ApiUrl
            });

            var login = await anonymous.PostAsync("/api/auth/login", new APIRequestContextOptions
            {
                DataObject = new { usernameOrEmail = user.Username, password = AppFixture.Password }
            });

            Assert.True(login.Ok, $"{user.Username} icin giris basarisiz: {login.Status}");

            var token = JsonDocument.Parse(await login.TextAsync()).RootElement
                .GetProperty("token").GetString();

            return await playwright.APIRequest.NewContextAsync(new()
            {
                BaseURL = AppFixture.ApiUrl,
                ExtraHTTPHeaders = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {token}"
                }
            });
        }

        private async Task AddToWatchlistAsync(TestUser user)
        {
            var api = await ApiAsync(_app.Playwright, user);

            var response = await api.PostAsync("/api/watchlist/add", new APIRequestContextOptions
            {
                DataObject = new
                {
                    id = CatalogSeeder.ShowTmdbId,
                    name = CatalogSeeder.ShowName,
                    overview = "",
                    posterPath = ""
                }
            });

            Assert.True(response.Ok, $"Watchlist'e eklenemedi: {response.Status}");
        }

        private async Task SetPrivateAsync(TestUser user, bool isPrivate)
        {
            var api = await ApiAsync(_app.Playwright, user);

            var response = await api.PutAsync("/api/users/me", new APIRequestContextOptions
            {
                DataObject = new { displayName = user.Username, isPrivate }
            });

            Assert.True(response.Ok, $"Profil guncellenemedi: {response.Status}");
        }
    }
}
