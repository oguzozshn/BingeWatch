# BingeWatch — Ürün Yol Haritası

**Vizyon:** Diziler için Letterboxd — kullanıcıların izledikleri dizileri bölüm bazında takip
ettiği, puanlayıp incelediği ve birbirini takip ederek keşif yaptığı sosyal bir platform.

Bu doküman mevcut kod tabanının analizini, hedef özellik setini ve faz faz uygulama planını içerir.

---

## 1. Mevcut Durum

*Son güncelleme: 27 Temmuz 2026, Faz 6.1 (moderasyon) sonu.*

İki ASP.NET Core projesi (.NET 10), tek solution:

| Katman | Durum |
|---|---|
| `BingeWatch.API` | TMDb proxy + katalog cache'i (`Shows`/`Seasons`/`Episodes`), Identity, kullanıcı katmanı (`UserShows`/`WatchedEpisodes`/`Ratings`/`Reviews`), SQL Server (LocalDB) |
| `BingeWatch.Web` | Blazor Server, cookie auth; **hiçbir dış API anahtarı kullanmıyor** — tüm TMDb erişimi API üzerinden |

**Çalışan özellikler**

- Kimlik: kayıt / giriş / çıkış, `/@kullaniciadi` profil sayfası
- Popüler diziler karuseli — [Series.razor](../BingeWatch.Web/Components/Pages/Series.razor)
- Dizi arama + watchlist ekle/çıkar — [WatchList.razor](../BingeWatch.Web/Components/Pages/WatchList.razor)
- Dizi detay + **bölüm bazlı izleme takibi** (tek bölüm / sezon / "buraya kadar"),
  ilerleme çubuğu, otomatik durum geçişleri — [ShowView.razor](../BingeWatch.Web/Components/Pages/ShowView.razor)
- Ana sayfa: **"Sırada ne var"** paneli + yaklaşan bölümler takvimi — [Home.razor](../BingeWatch.Web/Components/Pages/Home.razor)
- Arka planda TMDb senkronu (`TmdbSyncService`, 6 saatte bir)
- **Yarım yıldızlı puanlama** (dizi / sezon / bölüm) + kullanıcı ortalaması ve dağılım
  histogramı — [StarRating.razor](../BingeWatch.Web/Components/Shared/StarRating.razor)
- **İnceleme** (dizi + sezon) spoiler bayrağıyla; inceleme akışı — [Reviews.razor](../BingeWatch.Web/Components/Pages/Reviews.razor)
- Bölüm ısı haritası: TMDb ve kişisel puan katmanları toggle'lı
- Dizi sayfası sekmeli: Genel Bakış / Bölümler / İncelemeler / Benzer
- Takip, aktivite akışı, inceleme beğeni/yorumları, bildirimler (Faz 4)
- Listeler, filtreli keşif, gelişmiş arama, istatistik sayfası (Faz 5)
- **Moderasyon**: rate limiting, kullanıcı engelleme, içerik bildirimi ve
  `/admin/reports` paneli (Faz 6.1)

---

## 2. P0 — Devam Etmeden Önce Düzeltilmesi Gerekenler

> **Tarihsel kayıt.** Bu bölüm projenin ilk analizindeki (Faz 0 öncesi) durumu anlatır.
> §2.1–2.4'ün tamamı ve §2.5'in büyük kısmı Faz 0–2'de çözüldü; güncel açık maddeler
> için §7'ye bak.

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

Aşağıda ✅ olanlar kuruldu (Faz 1–2), işaretsizler hedef:

```
AspNetUsers (Identity) ─┬─ ✅ profil alanları AppUser üzerinde (DisplayName, Bio, AvatarUrl, IsPrivate)
                        ├─ ✅ Follow (FollowerId, FolloweeId, CreatedAt)
                        ├─ ✅ UserShow (ShowId, Status, StartedAt, CompletedAt, IsFavorite)
                        ├─ ✅ WatchedEpisode (EpisodeId, WatchedAt, RewatchNo)
                        ├─ ✅ Rating (TargetType: Show|Season|Episode, TargetId, Value 0.5–5)
                        ├─ ✅ Review (ShowId, SeasonNumber?, Body, HasSpoilers)
                        ├─ ✅ ReviewLike / ReviewComment
                        ├─ ✅ Notification (ActorId, Type, ReviewId?, UserListId?, ReadAt?)
                        ├─ ✅ UserList (Title, Description, IsPublic)
                        │       ├─ ✅ UserListItem (ShowId, Position, Note)
                        │       └─ ✅ UserListLike (UserId, CreatedAt)
                        ├─ ✅ ActivityEvent (Type, ShowId?, SeasonNumber?, EpisodeId?, EpisodeCount?,
                        │                    RatingValue?, ReviewId?, TargetUserId?, CreatedAt)
                        ├─ ✅ UserBlock (BlockerId, BlockedId, CreatedAt)
                        └─ ✅ Report (ReporterId, TargetType, TargetId?, TargetUserId, Reason,
                                      Note?, Status, ResolvedById?, ResolvedAt?, ResolutionNote?)

✅ Show (TmdbId, ImdbId, Name, Overview, PosterPath, BackdropPath, FirstAirDate,
         TmdbStatus, VoteAverage, VoteCount, LastSyncedAt)
 ├─ ✅ Genre (TMDb id'si birincil anahtar) — çoka çok
 ├─ ✅ Network (TMDb id'si birincil anahtar) — çoka çok
 └─ ✅ Season (ShowId, SeasonNumber, Name, AirDate, EpisodeCount)
     └─ ✅ Episode (SeasonId, EpisodeNumber, Name, AirDate, Runtime, StillPath, TmdbVoteAverage)
```

**Kritik karar: TMDb verisini yerel olarak cache'le.** ✅ *Faz 2'de uygulandı.* Eski tasarım
her istekte TMDb'ye gidiyordu; bölüm bazlı takip ve toplu istatistik bununla mümkün değildi.
`Show` / `Season` / `Episode` tabloları TMDb'den besleniyor ve `TmdbSyncService` ile periyodik
senkronize ediliyor. `WatchedEpisode` bu tablolara FK veriyor.

✅ `WatchListItem` → `UserShow`'a migrate edildi (Faz 2).

**Puan–inceleme bağı FK ile değil, hedef eşleşmesiyle kuruldu** (Faz 3). `Review` üzerinde
`RatingId` tutulmuyor; kartta gösterilen puan, yazarın *aynı hedefe* (dizi ya da sezon)
verdiği `Rating` satırından okunuyor. Böylece puan ile inceleme birbirinden bağımsız
silinip güncellenebiliyor — kullanıcı incelemeyi silince puanı kaybolmuyor.

`Rating.TargetId` polimorfik olduğu için (Show/Season/Episode) FK verilemiyor; hedefin
gerçekten o diziye ait olduğunu `RatingService.ResolveTargetAsync` doğruluyor.

---

## 5. Faz Faz Yol Haritası

### Faz 0 — Temizlik & Güvenlik (atlanamaz)

- [x] TMDb + OMDb anahtarlarını User Secrets'a taşı (`appsettings.json` artık boş) — ⚠️ anahtarların
      sağlayıcı tarafında **iptal edilip yenilendiği repodan doğrulanamaz**; yapılmadıysa git
      geçmişindeki eski anahtarlar hâlâ geçerli demektir
- [x] `.gitignore` düzelt (`.github/` kuralı kaldırıldı, CI eklenebiliyor)
- [x] Ölü kodu sil: `weatherforecast`, `Counter.razor`, `Weather.razor`, `Ping`, NavMenu linkleri
- [ ] `Console.WriteLine` → `ILogger`; debug markup'ını temizle — **kısmen**: API tarafı `ILogger`'a
      geçti; `ShowView.razor` Faz 2'de yeniden yazılırken temizlendi. Geriye yalnızca
      [WatchList.razor](../BingeWatch.Web/Components/Pages/WatchList.razor) kaldı (6 adet)
- [x] `IDisposable`, `App.razor` CSS yolu, `PosterPath` tutarsızlığını düzelt
- [x] NuGet paketlerini preview'dan stable'a al (EF Core / OpenAPI → 10.0.10)
- [x] `BingeWatch.Tests` projesi (xUnit) + `.github/workflows/ci.yml` (build + test)
- [x] `.editorconfig`, exception middleware + ProblemDetails

### Faz 1 — Kimlik & Kullanıcı

- [x] ASP.NET Core Identity — API'de `AppUser : IdentityUser`; Web tarayıcıya **cookie auth** sunuyor, Web→API arası JWT (cookie claim'i içinde taşınıyor, BFF benzeri)
- [x] Kayıt / giriş / çıkış (şifre sıfırlama henüz yok — sonraki iterasyon)
- [x] `UserProfile` alanları AppUser üzerinde (DisplayName, Bio, AvatarUrl, IsPrivate) + `/@username` profil sayfası
- [x] `WatchListController` `[Authorize]`; `userId` route'tan değil `ClaimsPrincipal`'dan (`ClaimTypes.NameIdentifier`) alınıyor
- [x] `WatchListItem` → `UserShow` migration'ı — **Faz 2'de yapıldı**: `WatchListItem` tablosu
      tamamen kaldırıldı, veri `Shows` + `UserShows`'a taşındı. `UserId` artık `AspNetUsers`'a
      FK; kimlik doğrulama öncesinden kalan sahte `"user1"` satırları migration'da elendi
- [x] `AuthorizeRouteView` + `AddCascadingAuthenticationState`, NavMenu kullanıcı menüsü (giriş/kayıt/çıkış + kullanıcı adı linki)

### Faz 2 — TMDb Cache & Bölüm Takibi (asıl değer)

> Durum: PR [#7](https://github.com/oguzozshn/BingeWatch/pull/7) — merge bekliyor.

- [x] `Show` / `Season` / `Episode` tabloları + TMDb'den doldurma servisi (`ShowCatalogService`;
      biten diziler 7 gün, devam edenler 12 saat TTL ile bayatlar)
- [x] `IMemoryCache` ile TMDb yanıt cache'i (popüler 30dk / arama 5dk); Web'deki doğrudan
      TMDb çağrıları tamamen kaldırıldı — Web artık hiçbir dış API anahtarı kullanmıyor
- [x] `TmdbSyncService` — `BackgroundService`, 6 saatte bir devam eden dizileri günceller
- [x] Bölüm işaretleme UI'ı: tek bölüm ✓, "sezonu izledim" ✓, "buraya kadar" ✓;
      katlanabilir sezon akordiyonu **Faz 3'te** eklendi (varsayılan kapalı, ilk yarım
      kalmış sezon açık başlar)
- [x] `Status` + ilerleme çubuğu (ilk işaretlemede İzliyorum, hepsi bitince Bitirdim;
      işaret kaldırılınca geri düşer — Bıraktım/Ertelendi korunur)
- [x] Ana sayfayı **"Sırada ne var"** paneline dönüştür
- [x] Yaklaşan bölümler takvimi (`/api/progress/upcoming?days=`)
- [x] OMDb bağımlılığını kaldır — TMDb tek kaynak

### Faz 3 — Puanlama & İnceleme

- [x] `Rating` (dizi/sezon/bölüm) + yarım yıldızlı reusable `<StarRating>` komponenti —
      hedef polimorfik (`TargetType` + `TargetId`); istemci yalnızca TMDb dizi id'si +
      seviye gönderir, yerel id'ye `RatingService` çevirir
- [x] `Review` CRUD + spoiler bayrağı ve gizleme davranışı (gövde varsayılan olarak perdeli)
- [x] Bölüm ısı haritasına **kişisel puan katmanı** (TMDb katmanı yanına toggle) —
      [EpisodeHeatmap.razor](../BingeWatch.Web/Components/Shared/EpisodeHeatmap.razor)
- [x] Dizi sayfası yeniden tasarımı: Genel Bakış / Bölümler / İncelemeler / Benzer sekmeleri
      (`/api/shows/{id}/similar` eklendi; sekme içerikleri ilk açılışta çekilir)
- [x] Kullanıcı puan ortalaması + dağılım histogramı (10 kova, boşlar 0 olarak döner)
- [x] İnceleme akışı sayfası (`/reviews`) + sıralama (en yeni / en eski / en yüksek puan)

### Faz 4 — Sosyal

> Üç PR'a bölündü: **(1)** takip altyapısı, **(2)** aktivite akışı, **(3)** etkileşim
> (beğeni/yorum/bildirim + profil zenginleştirme).

- [x] `Follow` + takip/takipçi sayfaları — tek yönlü takip; `/@kullanici/followers` ve
      `/@kullanici/following`, profilde sayaçlar ve `<FollowButton>`. Kendini takip
      engellendi, tekrar takip idempotent. **Gizlilik kuralı:** `IsPrivate` profiller
      yalnızca sahibine görünür — takip edilemez, listeleri okunamaz (takip isteği akışı yok)
- [x] `ActivityEvent` yazımı + fan-out okuma — olaylar puan/inceleme/izleme/takip
      servislerinden yazılıyor, akış okumada takip edilenlerin olayları toplanıyor.
      Puan ve inceleme güncellemesi yeni olay üretmez, mevcut olayı tazeler; kaynak
      kayıt silinince olay da silinir. Toplu bölüm işaretleme tek olay yazar
      (`EpisodeCount` + son bölüm), akış bölüm bölüm dolmuyor
- [x] Aktivite akışı (`/feed`), `Virtualize` ile sonsuz kaydırma — akışta kullanıcının
      kendi olayları da var; kimseyi takip etmeyen boş sayfa görmesin
- [x] `ReviewLike`, `ReviewComment` — beğeni butonu inceleme kartında, yorumlar
      thread'siz ve tek seviye; yorumu sahibi ya da inceleme sahibi silebilir
      (asgari moderasyon). Yorumlar ancak açıldığında çekilir
- [x] Dizi sayfasında "takip ettiklerinin puanı" — yalnızca dizi seviyesindeki puanlar
- [x] `Notification` + navbar göstergesi — takip, inceleme beğenisi ve yorumu bildirim
      üretir; kendi eylemin bildirim üretmez, eylem geri alınınca bildirim silinir.
      Zil sayacı sayfa yüklenirken bir kez okunur (canlı push Faz 6'da)
- [x] Profil zenginleştirme: favori diziler (ilk 4'ü gösterilir, dizi sayfasından
      işaretlenir), istatistik kartları, yıllara göre izlenen bölüm grafiği

### Faz 5 — Listeler & Keşif

> Faz 4 gibi PR'lara bölündü: **(1)** liste altyapısı, **(2)** beğeni + keşif,
> **(3)** filtreli keşif ve gelişmiş arama, **(4)** istatistik sayfası.

- [x] `UserList` CRUD, sıralı öğeler, öğe notları, gizlilik — `/@kullanici/lists` ve
      `/list/{id}`; sıra kalıcı (`Position`), öğe silinince boşluk sıkıştırılır, aynı
      dizi bir listeye iki kez eklenemez. **Gizlilik iki katmanlı:** kapalı liste
      yalnızca sahibine görünür, gizli profilin açık listeleri de dışarıya kapalıdır
      (Faz 4'teki kuralla aynı). Dizi sayfasındaki `<AddToListButton>` menüsü listeleri
      ve üyeliği tek istekte çeker, menüden yeni liste de açılabilir
- [x] Liste beğenme + keşif sayfası — `UserListLike` + `/lists` keşif akışı (en yeni /
      en beğenilen / en kapsamlı). Beğeni görebilmeyi gerektirir: kapalı liste ya da
      gizli profilin listesi beğenilemez. Keşifte **boş listeler görünmez** — postersiz
      ve bilgisiz kart işe yaramıyor. Beğeni `Notification`'a `ListLiked` türüyle
      giriyor (`Notification.UserListId` eklendi); kendi listeni beğenmek bildirim
      üretmez, beğeni geri alınınca bildirim silinir, liste silinince de temizlenir
- [x] Filtreli keşif (tür, platform, yıl, puan, durum) — `/discover` iki modlu:
      **"Tüm diziler"** TMDb `/discover/tv` üzerinden (yerel katalog yalnızca
      dokunulmuş dizileri içerir, onun üstünde filtre keşif değil rastgele bir alt
      küme olurdu), **"Kütüphanem"** tamamen yerel ve durum filtresini o mod açar.
      Tür filtresi "ve", platform "veya". Puana göre sıralamada `vote_count.gte=50`
      — tek oylu diziler listeyi çöpe çeviriyordu. Yanıtlar 15 dk cache'li
- [x] Gelişmiş arama (kişi/oyuncu dahil) — `/search` dizi ve kişiyi tek yanıtta
      döner, `/person/{id}` kişinin dizilerini karakter/görev ve bölüm sayısıyla
      listeler. "Bilinen dizileri" ipucu kişi başına ek istek demek; yalnızca ilk
      üç kişi için çekiliyor
- [x] İstatistik sayfası — izlenen süre, tür dağılımı, yıllık grafik: `/@kullanici/stats`
      (`/stats/detail` ucu profil bloğundan ayrı; tür dağılımı ve "en çok izlenenler"
      gibi ağır sorgular yalnızca bu sayfa açıldığında çalışsın). Süresi bilinmeyen
      bölümler toplam süreye girmez ama sayısı ayrıca gösterilir — eksiklik
      saklanmıyor. Bir dizi birden çok türe ait olabildiği için bölümler her türe
      ayrı sayılır (sayfada da yazıyor). Tür verisi katalog satırlarından sonra
      eklendiği için `BackfillCatalogGenres` migration'ı `LastSyncedAt`'i sıfırlar,
      mevcut diziler ilk erişimde yeniden senkronlanıp türlerini alır

### Faz 6 — Sağlamlaştırma & Yayın

> Faz 4–5 gibi PR'lara bölündü: **(1)** moderasyon & güvenlik, **(2)** performans,
> **(3)** mobil & erişilebilirlik, **(4)** SEO, **(5)** altyapı, **(6)** test.

- [x] Rate limiting, moderasyon araçları, engelleme, içerik bildirimi
  - **Rate limiting** — üç politika: giriş/kayıt IP başına 10/5dk (Identity'nin
    lockout'u tek hesabı korur, bu politika hesap taramasını yavaşlatır), yazma
    uçları kullanıcı başına 30/dk, bildirim 10/saat. Üstünde 120/dk'lık genel
    tavan var. Kimliği olan kullanıcı kendi kotasını harcar, anonim istek IP
    kotasını. 429 yanıtı `Retry-After` başlığıyla dönüyor — bunsuz arayüz sıkı
    yeniden deneme döngüsüne giriyor
  - **Engelleme** (`UserBlock`) — tek yönlü kaydedilir, **iki yönlü** etki eder:
    profil, istatistik, listeler, incelemeler, yorumlar, akış ve bildirimler her
    iki yönde de kapanır. Engel anında aradaki takipler, o takiplerin akış
    olayları ve takip bildirimleri temizlenir; engeli kaldırmak takipleri geri
    getirmez. **Hangi yönde engellendiği sızmasın diye ikisine de 404 dönüyor**
    — bu yüzden engeli kaldırmanın tek yeri `/settings/blocks`, karşı profil
    değil. Beğeniyi geri almak engel sonrası da mümkün (tek yönlü temizlik)
  - **İçerik bildirimi** (`Report`) — inceleme / yorum / liste / kullanıcı
    hedefli, sebep kodlu. Bildirim hedefin sahibini kopyalayarak saklar; içerik
    silindikten sonra da "bu kullanıcı hakkında kaç bildirim var" cevaplanabilsin
    diye. Aynı kullanıcı aynı hedefi ikinci kez bildiremez (kuyruk şişer)
  - **Moderasyon paneli** — `/admin/reports`, `Admin` Identity rolü. Rol JWT'ye
    claim olarak giriyor ve Web tarafında cookie'ye kopyalanıyor. Rol atama
    yalnızca `Admin:Usernames` yapılandırmasından, açılışta: panelden panel
    yetkisi dağıtılamıyor. Bir içerik hakkındaki karar (sil / reddet) o içeriğe
    ait diğer açık bildirimleri de kapatır
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

## 7. Bilinen Sorunlar / Doğrulanacaklar

### 7.1 Açık sorunlar

**Rolsüz `[Authorize]` sayfaları anonim ziyaretçiyi yönlendirmiyor.** `/feed`,
`/watchlist`, `/settings/blocks` gibi sayfalar giriş yapmamış ziyaretçiye boş
kabuk olarak çiziliyor; `<RedirectToLogin>` statik SSR sırasında tetiklenmiyor.
Rol gerektiren `/admin/reports` doğru yönlendiriyor. **Güvenlik açığı değil** —
API tarafı 401 döndüğü için veri sızmıyor, yalnızca kötü bir karşılama. Faz 6.1
öncesinden var, Faz 6.3'te (mobil & erişilebilirlik) düzeltilecek.

**WatchList arama butonu — blur yarışı.** "Search" butonuna tıklamak input'u blur ediyor;
`OnSearchBlur`'ün 200 ms'lik gizleme zamanlayıcısı, arama sonuçlarının gelişiyle yarışıyor.
Yazarak arama (debounce yolu) sorunsuz çalışıyor. Düzeltme: blur yerine `@onfocusout`
ile ilgili elemanı kontrol et ya da dropdown'a `@onmousedown:preventDefault` ekle.

### 7.2 Faz 3'te çözülenler

- ✅ **Sezonlar katlanamıyor**: ShowView'daki sezonlar artık akordiyon; varsayılan kapalı,
  ilk yarım kalmış sezon açık başlar.

### 7.3 Faz 2'de çözülenler

- ✅ **Kalp butonu** (eski §7.1): kök neden doğrulandı — Web `SeriesDto.FirstAirDate` `string`,
  API `DateTime?` bekliyordu ve `ShowYear` çıplak yıl (`"2008"`) gönderiyordu. ShowView'ın
  katalog API'sine taşınmasıyla kökten çözüldü, tarayıcıda doğrulandı.
- ✅ **Durum takılması**: tüm bölümler izlendikten sonra bir bölümün işareti kaldırılınca
  dizi sonsuza dek "Bitirdim"de kalıyordu.
- ✅ **TMDb boş tarih → arama çökmesi**: TMDb, tarihi bilinmeyen yapımlar için
  `first_air_date` alanını `""` döndürüyor; tek bozuk kayıt tüm arama isteğini 500 ile
  düşürüyordu. `NullableDateTimeConverter` eklendi.
- ✅ **N+1 `external_ids` çağrıları** (§2.3) ve **`WatchListItem` → `UserShow` migration'ı**.

### 7.4 Tamamlanmamış / ertelenen maddeler

**Faz 0'dan kalan**

- [WatchList.razor](../BingeWatch.Web/Components/Pages/WatchList.razor)'daki `Console.WriteLine`
  çağrıları `ILogger`'a taşınmadı (6 adet)
- API anahtarlarının sağlayıcı tarafında iptal/yenileme durumu doğrulanmadı (bkz. Faz 0)

**Faz 1'de bilinçli ertelenen**

- Şifre sıfırlama akışı

---

## 8. Riskler

- **TMDb rate limit** (~50 req/s) — cache ve senkron servis olmadan bölüm bazlı takip ölçeklenmez
- **Spoiler yönetimi** diziye özgü ve zor bir UX problemi; Faz 3'te baştan doğru modellenmeli, sonradan eklenmesi zor
- **`WatchListItem` migration'ı** tek seferlik ve geri dönüşü zahmetli — Faz 1'de yedekle
