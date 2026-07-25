# BingeWatch — Ürün Yol Haritası

**Vizyon:** Diziler için Letterboxd — kullanıcıların izledikleri dizileri bölüm bazında takip
ettiği, puanlayıp incelediği ve birbirini takip ederek keşif yaptığı sosyal bir platform.

Bu doküman mevcut kod tabanının analizini, hedef özellik setini ve faz faz uygulama planını içerir.

---

## 1. Mevcut Durum

İki ASP.NET Core projesi (.NET 10), tek solution:

| Katman | Durum |
|---|---|
| `BingeWatch.API` | TMDb proxy + SQL Server (LocalDB), tek tablo: `WatchListItems` |
| `BingeWatch.Web` | Blazor Server (InteractiveServer), Bootstrap, named `HttpClient` ile API'ye bağlanır |

**Çalışan özellikler**

- Popüler diziler karuseli — [Series.razor](../BingeWatch.Web/Components/Pages/Series.razor)
- Dizi arama + watchlist ekle/çıkar — [WatchList.razor](../BingeWatch.Web/Components/Pages/WatchList.razor)
- Dizi detay + sezon/bölüm ısı haritası, TMDb öncelikli / OMDb fallback — [ShowView.razor](../BingeWatch.Web/Components/Pages/ShowView.razor)

**Sosyal ürün açısından eksik olanlar**

Kullanıcı hesabı (`UserId = "user1"` sabit), puanlama, inceleme, takip, aktivite akışı,
bölüm bazlı izleme takibi, listeler, profil — hiçbiri yok.

---

## 2. P0 — Devam Etmeden Önce Düzeltilmesi Gerekenler

Bunlar "iyileştirme" değil; üstüne inşa edilemeyecek temel problemler.

### 2.1 API anahtarları git'e commit'lenmiş — rotasyon gerekir

- [BingeWatch.API/appsettings.json](../BingeWatch.API/appsettings.json) → TMDb Bearer token
- [BingeWatch.Web/appsettings.json](../BingeWatch.Web/appsettings.json) → OMDb + TMDb v3 key

`.gitignore` yalnızca `appsettings.Development.json`'ı dışlıyor; asıl anahtarlar açıkta.
**Her iki anahtar da iptal edilip yenilenmeli**, sonra User Secrets / environment variable'a
taşınmalı. Ayrıca `.gitignore` sonundaki `.github/` kuralı CI eklemeyi imkânsız kılıyor.

### 2.2 Kimlik doğrulama yok

Sosyal katmanın tamamı gerçek `UserId`'ye bağlı. Şu an hardcode:
`ShowView.razor` ve `WatchList.razor` içinde `private const string UserId = "user1"`.
API'de hiç `[Authorize]` yok ve `userId` route parametresinden geliyor → herhangi biri
başkasının watchlist'ini okuyup değiştirebilir.

### 2.3 Web katmanı TMDb'yi doğrudan çağırıyor — N+1

Her poster için ayrı `external_ids` isteği (`MapExternalIds`, hem Series hem WatchList
sayfasında). 20 dizi = 20 seri HTTP çağrısı, sayfa açılışını bloke ediyor. TMDb erişimi
tamamen API'ye taşınmalı ve cache'lenmeli.

### 2.4 `PosterPath` tutarsızlığı — kırık görseller

`ShowView` watchlist'e **tam URL** yazıyor, `WatchList` ise okurken başına
`https://image.tmdb.org/t/p/w500` ekliyor. ShowView'dan eklenen dizilerin posteri
watchlist'te kırık görünür. Sözleşme netleşmeli: DB'de her zaman TMDb relative path.

### 2.5 Diğer somut hatalar

- `Dispose()` yazılmış ama `@implements IDisposable` yok → hiç çağrılmıyor (Series, WatchList)
- `App.razor` `BingeOn.Web.styles.css` arıyor, assembly adı `BingeWatch.Web` → scoped CSS yüklenmiyor
- `WatchList.razor` içinde bozuk encoding'li debug metni + ekranda `ImdbId` gösteren debug `<h4>`
- `WatchListItem.IsInWatchList` kolonu hiç kullanılmıyor (satırın varlığı zaten anlamı)
- `ToggleAsync` remove hatasıyla başarılı remove'u ayırt etmiyor, ikisi de `false` döner
- `Console.WriteLine` ile loglama → `ILogger`
- Ölü kod: `/weatherforecast`, `Counter.razor`, `Weather.razor`, `Ping` endpoint'i, NavMenu linkleri
- `Microsoft.EntityFrameworkCore.*` ve `Microsoft.AspNetCore.OpenApi` **10.0.0-preview.5** → stable
- `Program.cs` içinde `context.Database.Migrate()` — deploy'da ayrı adım olmalı
- Hiç test projesi yok

---

## 3. Hedef Özellik Seti

Letterboxd'un dizilere uyarlanmasında kritik fark: **film tek bir nesne, dizi hiyerarşik.**
Puanlama üç seviyede olabilir (dizi / sezon / bölüm).

**Karar:** İnceleme (review) *dizi ve sezon* seviyesinde, puan *üç seviyede*, izleme takibi
*bölüm* seviyesinde. Bölüm bazlı yazılı inceleme, "bugün ne izledim" akışını spoiler
çöplüğüne çevirir.

### A. Takip / Kişisel Katman (ürünün belkemiği)

| Özellik | Not |
|---|---|
| Bölüm bazlı izleme takibi | Bölüm ✓, "sezonu izledim", "buraya kadar izledim" toplu işaretleme |
| İlerleme durumu | Dizi bazında: İzliyorum / Bitirdim / Bıraktım / İzleyeceğim |
| "Sırada ne var" paneli | Ana sayfa merkezi: izlenen son bölümden sonraki bölüm |
| Takvim / yayın akışı | Takip edilen dizilerin yaklaşan bölümleri |
| İzleme geçmişi + istatistik | Toplam süre, yıla göre dağılım, tür dağılımı, en çok izlenen |
| Yeniden izleme (rewatch) | Aynı bölümü birden çok kez, tarihli |

### B. Puanlama & İnceleme

| Özellik | Not |
|---|---|
| 5 yıldız (yarım yıldız) puan | Dizi, sezon ve bölüm seviyesinde |
| Yazılı inceleme | Dizi + sezon; spoiler bayrağı zorunlu |
| Kişisel bölüm ısı haritası | Mevcut TMDb haritasının yanına *kendi* puanların — imza özellik adayı |
| Beğeni (like) | İnceleme ve listelerde |
| Etiketleme | Kendi tag'leri (`comfort-show`, `bırakılan`) |

### C. Sosyal Katman

| Özellik | Not |
|---|---|
| Kullanıcı profili | `/@kullaniciadi` — istatistik, favori 4 dizi, son aktivite |
| Takip et / takipçi | Tek yönlü (Letterboxd modeli) |
| Aktivite akışı | Takip edilenlerin puan / inceleme / bölüm aktiviteleri |
| İnceleme yorumları | Thread'siz, tek seviye |
| Arkadaş puanları | Dizi sayfasında "takip ettiklerinin ortalaması" |
| Listeler | Sıralı, açıklamalı, herkese açık/özel, beğenilebilir |
| Bildirimler | Takip, beğeni, yorum |
| Paylaşım kartları | OG image ile dışa paylaşım |

### D. Keşif

Tür / platform / yıl / puan filtreleri, "arkadaşların izlediği", benzer diziler,
popülerlik trendleri, gelişmiş arama.

### E. Moderasyon (gerekli, atlanamaz)

Spam bildirimi, kullanıcı engelleme, rate limiting, temel admin paneli.

---

## 4. Hedef Veri Modeli

Mevcut tek tablodan çıkıp şu yapıya gitmek gerekiyor:

```
AspNetUsers (Identity) ─┬─ UserProfile (Username, DisplayName, Bio, AvatarUrl, IsPrivate)
                        ├─ Follow (FollowerId, FolloweeId, CreatedAt)
                        ├─ UserShow (ShowId, Status, StartedAt, CompletedAt, IsFavorite)
                        ├─ WatchedEpisode (EpisodeId, WatchedAt, RewatchNo)
                        ├─ Rating (TargetType: Show|Season|Episode, TargetId, Value 0.5–5)
                        ├─ Review (ShowId, SeasonNumber?, Body, HasSpoilers, RatingId?)
                        ├─ ReviewLike / ReviewComment
                        ├─ UserList ─── UserListItem (ShowId, Order, Note)
                        ├─ ActivityEvent (Type, TargetId, CreatedAt)   ← akış için denormalize
                        └─ Notification

Show (TmdbId, ImdbId, Name, Overview, PosterPath, FirstAirDate, Status, LastSyncedAt)
 └─ Season (ShowId, SeasonNumber, EpisodeCount)
     └─ Episode (SeasonId, EpisodeNumber, Name, AirDate, Runtime, TmdbVoteAverage)
```

**Kritik karar: TMDb verisini yerel olarak cache'le.** Şu anki tasarım her istekte TMDb'ye
gidiyor; bölüm bazlı takip ve toplu istatistik bununla mümkün değil. `Show` / `Season` /
`Episode` tabloları TMDb'den beslenip periyodik senkronize edilmeli (background service).
`WatchedEpisode` bu tablolara FK verir.

`WatchListItem` → `UserShow` + `Rating`'e migrate edilir; TMDb `Id` her yerde tekil dış
anahtar olduğu için veri kaybı olmaz.

---

## 5. Faz Faz Yol Haritası

### Faz 0 — Temizlik & Güvenlik (atlanamaz)

- [ ] TMDb + OMDb anahtarlarını **iptal et ve yenile**; User Secrets / env var'a taşı
- [ ] `.gitignore` düzelt (`.github/` kuralını kaldır)
- [ ] Ölü kodu sil: `weatherforecast`, `Counter.razor`, `Weather.razor`, `Ping`, NavMenu linkleri
- [ ] `Console.WriteLine` → `ILogger`; debug markup'ını temizle
- [ ] `IDisposable`, `App.razor` CSS yolu, `PosterPath` tutarsızlığını düzelt
- [ ] NuGet paketlerini preview'dan stable'a al
- [ ] `BingeWatch.Tests` projesi (xUnit) + `.github/workflows/ci.yml` (build + test)
- [ ] `.editorconfig`, exception middleware + ProblemDetails

### Faz 1 — Kimlik & Kullanıcı

- [ ] ASP.NET Core Identity + **cookie auth** (JWT değil — gerekçe §6)
- [ ] Kayıt / giriş / çıkış / şifre sıfırlama
- [ ] `UserProfile` + benzersiz kullanıcı adı, `/@username` profil sayfası
- [ ] Tüm endpoint'lere `[Authorize]`; `userId`'yi route'tan değil **`ClaimsPrincipal`'dan** al (§2.2 açığını kapatır)
- [ ] `WatchListItem` → `UserShow` migration'ı (mevcut `user1` verisini ilk hesaba bağla)
- [ ] `AuthorizeRouteView` + `CascadingAuthenticationState`, NavMenu kullanıcı menüsü

### Faz 2 — TMDb Cache & Bölüm Takibi (asıl değer)

- [ ] `Show` / `Season` / `Episode` tabloları + TMDb'den doldurma servisi
- [ ] `HybridCache` / `IMemoryCache` ile TMDb yanıt cache'i; Web'deki doğrudan TMDb çağrılarını kaldır
- [ ] `TmdbSyncService` — `BackgroundService`, yeni bölümleri günceller
- [ ] Bölüm işaretleme UI'ı: sezon akordiyonu, tek bölüm ✓, "sezonu izledim", "buraya kadar"
- [ ] `Status` + ilerleme çubuğu
- [ ] Ana sayfayı **"Sırada ne var"** paneline dönüştür (şu an boş "Hello, world!")
- [ ] Yaklaşan bölümler takvimi
- [ ] OMDb bağımlılığını kaldır — TMDb tek kaynak

### Faz 3 — Puanlama & İnceleme

- [ ] `Rating` (dizi/sezon/bölüm) + yarım yıldızlı reusable `<StarRating>` komponenti
- [ ] `Review` CRUD + spoiler bayrağı ve gizleme davranışı
- [ ] Bölüm ısı haritasına **kişisel puan katmanı** (TMDb katmanı yanına toggle)
- [ ] Dizi sayfası yeniden tasarımı: Genel Bakış / Bölümler / İncelemeler / Benzer sekmeleri
- [ ] Kullanıcı puan ortalaması + dağılım histogramı
- [ ] İnceleme akışı sayfası (`/reviews`) + sıralama

### Faz 4 — Sosyal

- [ ] `Follow` + takip/takipçi sayfaları
- [ ] `ActivityEvent` yazımı + fan-out okuma
- [ ] Aktivite akışı (`/feed`), `Virtualize` ile sonsuz kaydırma
- [ ] `ReviewLike`, `ReviewComment`
- [ ] Dizi sayfasında "takip ettiklerinin puanı"
- [ ] `Notification` + navbar göstergesi
- [ ] Profil zenginleştirme: favori 4 dizi, istatistik kartları, yıllık özet

### Faz 5 — Listeler & Keşif

- [ ] `UserList` CRUD, sıralı öğeler, öğe notları, gizlilik
- [ ] Liste beğenme + keşif sayfası
- [ ] Filtreli keşif (tür, platform, yıl, puan, durum)
- [ ] Gelişmiş arama (kişi/oyuncu dahil)
- [ ] İstatistik sayfası — izlenen süre, tür dağılımı, yıllık grafik

### Faz 6 — Sağlamlaştırma & Yayın

- [ ] Rate limiting, moderasyon araçları, engelleme, içerik bildirimi
- [ ] Cursor-based sayfalama, N+1 denetimi, DB indeksleri
- [ ] Mobil responsive + erişilebilirlik (klavye, ARIA, kontrast)
- [ ] OG meta + prerender (SEO — Letterboxd trafiğinin büyük kısmı buradan gelir)
- [ ] Docker + gerçek SQL Server (LocalDB'den çıkış), Serilog + health check
- [ ] E2E test (Playwright), yük testi

---

## 6. Teknik Kararlar

**Blazor Server kalsın mı?** Evet. Sosyal akış ve bildirimler zaten SignalR bağlantısından
faydalanır. Ancak dizi ve inceleme sayfaları **SEO için** prerender edilmeli (Faz 6). Uzun
vadede yalnızca bu sayfalar için static SSR + interactive island modeli düşünülebilir.

**Auth: cookie mi JWT mi?** Cookie. Blazor Server aynı origin ailesinde çalıştığı ve token'ı
tarayıcıda tutma ihtiyacı olmadığı için JWT'den hem daha basit hem daha güvenli.

**API ayrı kalsın mı?** Kısa vadede evet, ama Web'in TMDb'ye doğrudan gitmesi tamamen
bitmeli. Tek geliştiricili projede iki proje deploy karmaşıklığı yaratıyor — Faz 6'da tek
host altında birleştirme (aynı process, `/api` prefix) ciddi olarak değerlendirilmeli.

**Sıralama:** Faz 0 → 1 → 2 kesinlikle sırayla. Faz 2 ürünün farklılaştırıcısı ve sosyal
katmanın besleyeceği veriyi üretir; sosyalliği (Faz 4) ondan önce yapmak boş bir akışla
sonuçlanır. Faz 3 ve 5 birbirinden bağımsız, paralel gidilebilir.

---

## 7. Riskler

- **TMDb rate limit** (~50 req/s) — cache ve senkron servis olmadan bölüm bazlı takip ölçeklenmez
- **Spoiler yönetimi** diziye özgü ve zor bir UX problemi; Faz 3'te baştan doğru modellenmeli, sonradan eklenmesi zor
- **`WatchListItem` migration'ı** tek seferlik ve geri dönüşü zahmetli — Faz 1'de yedekle
