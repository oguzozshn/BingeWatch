# BingeWatch — Ürün Yol Haritası

**Vizyon:** Diziler için Letterboxd — kullanıcıların izledikleri dizileri bölüm bazında takip
ettiği, puanlayıp incelediği ve birbirini takip ederek keşif yaptığı sosyal bir platform.

Bu doküman mevcut kod tabanının analizini, hedef özellik setini ve faz faz uygulama planını içerir.

---

## 1. Mevcut Durum

*Son güncelleme: 7 Ağustos 2026. Faz 0–8 tamamlandı. Docker x86-64'te
doğrulandı, profil düzenleme ve gizlilik anahtarı eklendi, Web katmanı test
altına alındı; oyun ve trivia sayfaları açıldı. Kalan açık maddeler §7.1'de,
hedef özellik setinden geriye yalnızca etiketleme kaldı (§7.6).*

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
- **Bölüm sayfası + bölüm tartışmaları** — `/show/{id}/season/{n}/episode/{m}`;
  iplik yalnızca o bölümü izlemiş olana açık ve hiçbir akışa düşmez (Faz 7)
- **Yeniden izleme (rewatch)** ve **"nerede kaldım" işareti** — ikisi de
  ilerleme modeline dokunmadan eklendi; yarıda kalan bölüm panelde öne geçer (Faz 7)
- **Profil düzenleme + gizlilik anahtarı** — `IsPrivate` artık kullanılabiliyor (Faz 8)
- **Kullanıcı arama** (`/search`) ve **oyunlar**: `/game` (hangisinin puanı
  yüksek) ve `/trivia` (üretilmiş sorular) — Faz 8
- Listeler, filtreli keşif, gelişmiş arama, istatistik sayfası (Faz 5)
- **Moderasyon**: rate limiting, kullanıcı engelleme, içerik bildirimi ve
  `/admin/reports` paneli (Faz 6.1)
- **Dizi sayfası anonime açık** — OG/Twitter kartları, schema.org `TVSeries`
  yapılandırılmış verisi, `robots.txt` ve katalogdan üretilen `sitemap.xml` (Faz 6.4)

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

> **Faz 7 notu:** Bu karar *inceleme* için hâlâ geçerli. Ama gerekçesi akışın
> kirlenmesiydi ve bu, akışa hiç düşmeyen bir tartışma biçimini dışlamıyor:
> Faz 7'de **bölüm tartışmaları** eklendi — yayılmayan, yalnızca bölümü izlemiş
> olana açılan iplikler. Ayrıntı Faz 7'de.

### A. Takip / Kişisel Katman (ürünün belkemiği)

| Özellik | Not |
|---|---|
| Bölüm bazlı izleme takibi | Bölüm ✓, "sezonu izledim", "buraya kadar izledim" toplu işaretleme |
| İlerleme durumu | Dizi bazında: İzliyorum / Bitirdim / Bıraktım / İzleyeceğim |
| "Sırada ne var" paneli | Ana sayfa merkezi: izlenen son bölümden sonraki bölüm; yarıda kalan bölüm öne geçer |
| Takvim / yayın akışı | Takip edilen dizilerin yaklaşan bölümleri |
| İzleme geçmişi + istatistik | Toplam süre, yıla göre dağılım, tür dağılımı, en çok izlenen |
| Yeniden izleme (rewatch) ✅ | Aynı bölümü birden çok kez, tarihli — Faz 7 |

### B. Puanlama & İnceleme

| Özellik | Not |
|---|---|
| 5 yıldız (yarım yıldız) puan | Dizi, sezon ve bölüm seviyesinde; bölüm puanı bölümü izlendi olarak da işaretler |
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
                        │       RewatchNo 0 = ilk izleme; >0 satırlar yalnızca
                        │       istatistiğe girer, ilerlemeye değil (Faz 7)
                        ├─ ✅ EpisodeBookmark (EpisodeId, PositionMinutes, UpdatedAt)
                        │       "yarıda bıraktım" — bölüm başına tek, izlenince
                        │       silinir; WatchedEpisode'u dışlar (Faz 7)
                        ├─ ✅ Rating (TargetType: Show|Season|Episode, TargetId, Value 0.5–5)
                        ├─ ✅ Review (ShowId, SeasonNumber?, Body, HasSpoilers)
                        ├─ ✅ ReviewLike / ReviewComment
                        ├─ ✅ EpisodeComment (EpisodeId, Body, CreatedAt) — okuması
                        │       WatchedEpisode'a bağlı; FK değil, servis kapısı (Faz 7)
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

- [x] TMDb + OMDb anahtarlarını User Secrets'a taşı (`appsettings.json` artık boş) —
      ✅ **28.07.2026'da doğrulandı:** git geçmişindeki (`137683d`) eski TMDb token'ı
      TMDb'ye sorulduğunda **401** dönüyor, yani gerçekten iptal edilmiş. OMDb anahtarı
      da artık ölü kod: `omdb` geçen tek dosya bu doküman, kodda tek referans yok.
      ⚠️ Ama "taşı" maddesinin yalnızca **sil** yarısı yapılmıştı: user-secrets deposu
      (`%APPDATA%\Microsoft\UserSecrets\`) bu makinede hiç oluşturulmamıştı. Anahtar
      25.07 16:31'de `appsettings.json`'dan silindiğinden beri API **açılamıyordu**
      (`Jwt:Key is not configured`). 28.07'de `Tmdb:ApiKey`, `Jwt:Key`, `Jwt:Issuer`,
      `Jwt:Audience` girildi. Depo proje klasörünün dışında olduğu için OneDrive ile
      senkronlanmıyor — yeni makinede baştan girilmesi gerekiyor
- [x] `.gitignore` düzelt (`.github/` kuralı kaldırıldı, CI eklenebiliyor)
- [x] Ölü kodu sil: `weatherforecast`, `Counter.razor`, `Weather.razor`, `Ping`, NavMenu linkleri
- [x] `Console.WriteLine` → `ILogger`; debug markup'ını temizle — API tarafı Faz 0'da,
      `ShowView.razor` Faz 2'de yeniden yazılırken, son 6 çağrı
      (`WatchList.razor`) Faz 6.3'te taşındı. Kod tabanında `Console.WriteLine`
      kalmadı *(kutu Faz 6.3'te işaretlenmemişti; 28.07'de doğrulanıp kapatıldı)*
- [x] `IDisposable`, `App.razor` CSS yolu, `PosterPath` tutarsızlığını düzelt
- [x] NuGet paketlerini preview'dan stable'a al (EF Core / OpenAPI → 10.0.10)
- [x] `BingeWatch.Tests` projesi (xUnit) + `.github/workflows/ci.yml` (build + test)
- [x] `.editorconfig`, exception middleware + ProblemDetails

### Faz 1 — Kimlik & Kullanıcı

- [x] ASP.NET Core Identity — API'de `AppUser : IdentityUser`; Web tarayıcıya **cookie auth** sunuyor, Web→API arası JWT (cookie claim'i içinde taşınıyor, BFF benzeri)
- [x] Kayıt / giriş / çıkış
- [x] Şifre sıfırlama — `/forgot-password` ve `/reset-password` sayfaları,
      Identity token üretimi, sıfırlama uçları ve SMTP göndericisi (MailKit).
      30.07.2026'da Gmail SMTP yapılandırıldı ve gerçek bir mailin uçtan uca
      teslim edildiği doğrulandı — ayrıntı §7.1
  - **Hesap sayımına kapalı:** e-posta kayıtlı olsun olmasın `/forgot-password`
    her zaman aynı 200 ve aynı ekranı veriyor. "Böyle bir kullanıcı yok" demek,
    adresleri tek tek deneyerek üyeleri saymaya izin verirdi. Aynı sebeple
    geçersiz token ile bilinmeyen hesap aynı hatayı döndürüyor
  - Token sorgu dizesinde taşındığı için base64url kodlanıyor; ham Identity
    token'ı `+` gibi karakterler içeriyor ve kodlanmadan bozuluyor
  - Sıfırlama sonrası `ResetAccessFailedCountAsync` çağrılıyor: kilitli hesap
    doğru parolayla bile giremeyip sebebini anlayamıyordu
  - Üretimde loglayan uygulamanın sessizce devreye girmemesi önemliydi — log,
    hesap ele geçirmeye yeten bağlantılarla dolardı. Ama **açılışta patlatmak da
    yanlıştı**: eksik olan tek bir özelliğin teslimatı, uygulamanın tamamı değil
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
- [x] **Kullanıcı arama** (02.08.2026'da eklendi) — aynı `/search` sayfasında,
      sonuçların en üstünde. **Faz 4'ün eksik kalan yarısıydı:** takip özelliği
      vardı ama birini bulmanın tek yolu `/@kullaniciadi` adresini elle yazmaktı,
      yani takip pratikte kullanılamıyordu
  - Kullanıcı araması yerelden, dizi/kişi TMDb'den geldiği için iki istek
    paralel atılıyor; birbirini beklemelerinin sebebi yok
  - **Gizli profiller hiç dönmüyor — sahibine bile.** Profil zaten başkasına
    açılmıyor; arama sonucunda görünmesi var olduğunu duyurmaktan başka işe
    yaramaz. Engelli taraflar `ProjectAsync`'in mevcut süzgecinden eleniyor
  - En az iki karakter isteniyor: tek harf tüm kullanıcıları dökerdi
  - Baştan eşleşenler önce sıralanıyor ("al" araması "alper"i "kemalist"in
    üstünde göstermeli)
  - Sonuç yoksa "Kullanıcılar" başlığı hiç çizilmiyor — dizi arayan birine
    "eşleşen kullanıcı yok" demek gürültü
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
- [x] Cursor-based sayfalama, N+1 denetimi, DB indeksleri
  - **İmleç** — akış, bildirimler, inceleme akışı, liste keşfi ve moderasyon
    kuyruğu `skip`/`take` yerine opak imleç alıyor; yanıt `{ items, nextCursor }`
    zarfına girdi. İmleç `(CreatedAt, Id)` çiftini taşıyor: akışa sürekli yeni
    satır girdiği için offset'te sayfa sınırındaki satırlar atlanıyor ya da
    tekrar ediyordu. **Sıralama anahtarı satırda durmayanlar** (inceleme "en
    yüksek puan", liste "en beğenilen"/"en kapsamlı") offset'te kaldı — imleç
    aynı zarfın içinde offset kodluyor, istemci farkı görmüyor. Bozuk/eski
    biçimli imleç 400 değil "listenin başı" demek
  - **Akış sayfası `<Virtualize>`'dan çıktı** — ItemsProvider satırı indeksle
    istiyor (`StartIndex`), imleçte indeks diye bir şey yok. "Daha fazla"
    düğmesine geçildi
  - **N+1** — `GetNextUpAsync` (ana sayfa "Sırada ne var") aktif dizi başına üç
    sorgu atıyordu ve kullanıcının **tüm** izleme geçmişini her dizi için baştan
    çekiyordu; 30 dizilik bir kullanıcıda 91 sorgu. Dizi sayısından bağımsız üç
    sorguya indi. Liste kartı önizlemesi 4 poster için listenin tüm öğelerini
    çekiyordu, ilk 12 öğeyle sınırlandı
  - **"En yüksek puan" sıralaması düzeltildi** — puan bellekte, sayfa çekildikten
    *sonra* sıralanıyordu; yani ikinci sayfadan itibaren "en yüksek puanlı"
    listesi değil, rastgele bir dilimin kendi içinde sıralanmışı geliyordu.
    Sıralama alt sorguyla veritabanına taşındı
  - **İndeksler** — `Reviews(CreatedAt, Id)`, `UserLists(UpdatedAt, Id)`,
    `UserShows(UserId, Status)`; `ActivityEvents`, `Notifications` ve `Reports`
    üzerindeki tarih indeksleri imleçle uyumlu olsun diye `Id` ile genişletildi
- [x] Mobil responsive + erişilebilirlik (klavye, ARIA, kontrast)
  - **Dil** — `<html lang="en">` idi; arayüzün tamamı Türkçeyken ekran okuyucu
    metni İngilizce fonemlerle okuyordu. `lang="tr"` yapıldı
  - **Klavye** — gezinti amaçlı beş `<div @@onclick>` gerçek `<a>`/`<button>`
    oldu (ana sayfa kartları ve yaklaşan bölümler, popüler karusel, watchlist
    kartı ve arama önerileri). Bunlar klavyeyle hiç erişilemiyor, orta tıkla
    yeni sekmede de açılmıyordu. Watchlist kartı ayrıca *tıklanabilir kartın
    içinde buton* barındırıyordu; gezinti başlığa, "Kaldır" yanına ayrıldı
  - **Odak görünürlüğü** — özel butonların (`.link-btn`, `.filter`,
    `.carousel-card`, `.report-link` …) hiçbirinin odak stili yoktu. Tek bir
    `:focus-visible` kuralı eklendi (fareyle tıklamada çıkmaz, klavyede çıkar)
  - **Atlama bağlantısı** — her sayfada tekrarlanan gezintiyi sekmeyle geçmek
    zorunda kalınmasın diye "İçeriğe atla"; yalnızca odakta görünür
  - **Başlık yapısı** — hiçbir sayfada `<h1>` yoktu, sayfalar `<h2>`/`<h3>` ile
    başlıyordu. 19 sayfanın ilk başlığı `<h1>`e çıkarıldı; global `h1` boyutu
    eski `h3`e sabitlendi ki tasarım bozulmasın
  - **Form etiketleri** — giriş/kayıt formlarındaki `<label>`ların `for`'u yoktu
    ve inputlar içlerinde değildi (ilişki hiç kurulmamıştı); yıl aralığındaki
    ikinci input, arama kutuları ve sıralama seçicileri de adsızdı. Hepsine
    `for`/`id` ya da `aria-label` verildi, parola alanlarına `autocomplete`
  - **ARIA** — `<StarRating>` düzenlenebilir halde `role="radio"` +
    `aria-checked` taşıyor (eskiden "10 buton" diye okunuyor, mevcut puan hiç
    bildirilmiyordu), salt okunur halde etiket değeri de söylüyor. Dizi
    sayfasındaki sekmeler tam ARIA desenine geçti: tek odak durağı, ok/Home/End
    ile gezinme, `aria-controls`/`aria-labelledby` ile panel bağı. Isı haritası
    hücreleri `role="img"` + `aria-label` aldı (`title` klavye ve ekran
    okuyucuya ulaşmıyordu). Bildirim zilinin erişilebilir adı okunmamış sayısını
    içeriyor. Dekoratif ikonlar `aria-hidden`
  - **Kontrast** — sabit griler (`#888`, `#8a8a8a`, `#9a9a9a`) kart zemininde
    AA'nın altındaydı, tema değişkenine bağlandı; `#d9534f` ve `#e0607e` metin
    olarak kullanıldıkları yerlerde açıldı
  - **Responsive** — filtre paneli, yaklaşan bölümler satırı ve poster ızgaraları
    dar ekrana uyarlandı. 375px'te poster ızgarası **1 piksel farkla** tek sütuna
    düşüyordu (295px alana `minmax(140px,…)` iki sütun sığdıramıyor), alt sınır
    düşürüldü. `pointer: coarse`'ta dokunma hedefleri 44px, `.link-btn` her
    yerde en az 24px (WCAG 2.5.8). `prefers-reduced-motion` desteği eklendi
  - Yol üstünde: WatchList'teki 6 `Console.WriteLine` `ILogger`'a taşındı
    (Faz 0'dan kalan madde) ve §7.1'deki **arama blur yarışı** kapandı —
    öneri listesine `@@onmousedown:preventDefault` eklendi
- [x] OG meta + prerender (SEO — Letterboxd trafiğinin büyük kısmı buradan gelir)
  - **Asıl engel prerender değildi, kimlik duvarıydı.** Blazor'ın `InteractiveServer`
    modu zaten prerender ediyordu; ama ürünün en çok aranan sayfası olan
    `/show/{id}` `[Authorize]` arkasındaydı — arama motoru hiçbir dizi sayfasını
    göremiyordu. Sayfa herkese açıldı, kişisel katman (puanın, ilerlemen,
    watchlist, favori, arkadaş puanları, inceleme formu, bölüm işaretleme)
    `<AuthorizeView>` arkasına alındı. İlgili API uçları zaten `[Authorize]`
    olduğu için anonimde o istekler hiç atılmıyor
  - **`<PageMeta>`** — başlık, açıklama, canonical, OG ve Twitter kart etiketleri
    tek komponentten. Canonical sorgu dizesini atıyor: filtre/sıralama
    parametreleri aynı içeriği farklı URL'lerde gösteriyordu, arama motoru
    bunları kopya sayardı. Kişisel sayfalar (`/notifications`,
    `/settings/blocks`, `/admin/reports`) `noindex`
  - **JSON-LD** — dizi sayfasında schema.org `TVSeries`: sezon/bölüm sayısı,
    yayın tarihi, poster ve `aggregateRating`. Oy yoksa puan bloğu hiç
    yazılmıyor (oysuz `aggregateRating` doğrulamada hata veriyor)
  - **robots.txt + sitemap.xml** — ikisi de statik dosya değil uç nokta; sitemap
    katalogdan üretiliyor (`/api/sitemap/*`) ve host adı istekten geliyor, aynı
    dosya yerel/staging/üretimde paylaşılamazdı. Kişisel ve filtre sayfaları
    taramaya kapalı. API'ye ulaşılamazsa sitemap boş değil **eksik** dönüyor:
    500 vermek arama motoruna "site bozuk" sinyali olurdu
  - İki tuzak: **aynı sayfada iki `<HeadContent>` olamıyor** (ikincisi ilkini
    eziyor — JSON-LD ayrı blokta yazılınca çıktıya hiç düşmedi, `PageMeta`'ya
    parametre olarak taşındı) ve `XmlWriter` bir `StringBuilder`'a yazarken
    bildirime `encoding="utf-16"` koyuyor ama yanıt UTF-8 gidiyor
- [x] Docker + gerçek SQL Server (LocalDB'den çıkış), Serilog + health check —
      arm64 (Pi 5) ve x86-64 (02.08.2026) mimarilerinin ikisinde de uçtan uca
      çalıştırıldı. Ayrıntı: [DEPLOY.md](DEPLOY.md)
  - **Sabit adresler kalktı** — Web, API adresini `Program.cs`'e gömülü
    `http://localhost:5054/` yerine `Api:BaseUrl`'den okuyor. Bu tek satır
    konteynerde çalışmayı imkânsız kılıyordu: compose ağında API "localhost"
    değil servis adıyla görünüyor. Bağlantı dizesi de `ConnectionStrings__*`
    ortam değişkeniyle eziliyor
  - **Serilog** — her iki uygulamada da stdout'a yapılandırılmış log; istek
    başına tek satır (`UseSerilogRequestLogging`). Framework'ün üç satırlık
    kendi logu, EF'in her SQL'i ve `HttpClient`'ın çağrı başına dört satırı
    `MinimumLevel:Override` ile bastırıldı. ⚠️ **Tuzak:** `Override` altındaki
    her anahtar logger kaynağı olarak okunuyor; oraya `_comment` koymak
    uygulamayı açılışta çökertiyor (bir kez düştü, düzeltildi)
  - **Health check** — `/health` liveness (hiçbir bağımlılığa bakmaz),
    `/health/ready` SQL Server'a gerçekten ulaşıyor mu. Ayrım bilinçli:
    veritabanı düşünce konteyneri yeniden başlatmak sorunu çözmez, yeniden
    başlatma döngüsüne sokar. İkisi de rate limiting'in dışında
  - **Migration** — açılışta uygulanıyor ama artık `Database:MigrateOnStartup`
    ile kapatılabiliyor ve bağlantı hatalarında yeniden deneniyor (SQL Server
    ile API konteynerde aynı anda ayağa kalkıyor). Birden çok kopya
    çalıştırılacaksa kapatılıp ayrı deploy adımına taşınmalı
  - **Docker** — API ve Web için çok aşamalı `Dockerfile` (root olmayan
    kullanıcı, restore ayrı katmanda), SQL Server 2022 ile `docker-compose.yml`,
    `.dockerignore`, `.env.example`. Sırlar `.env`'de ve git dışında; API ve
    veritabanı yalnızca `127.0.0.1`'e bağlı, dışarıya yalnızca Web açık
  - ~~**Doğrulanmadı:** `docker build` ve `docker compose up` hiç çalıştırılmadı.~~
    **02.08.2026'da çalıştırıldı ve doğrulandı** — aşağıdaki "ilk denemede
    kırılması en olası yerler" listesinin hiçbiri kırılmadı. Çıkan tek sorun
    listede olmayan bir şeydi: Linux'ta kültür (§7.1).
  - *(tarihsel)* `docker build` ve `docker compose up` hiç çalıştırılmamıştı.
    Compose şeması ayrıştırılarak doğrulandı, kod tarafı yerelde uçtan uca
    çalıştı. İlk denemede kırılması en olası yerler DEPLOY.md §4'te
- [x] E2E test (Playwright), yük testi — 23 test yeşil (anonim yüzey, girişli
      akışlar, şifre sıfırlama, bölüm tartışmaları), CI'da koşuyor; yük testi
      elle çalıştırılan teşhis aracı olarak kuruldu
      ([BingeWatch.LoadTest](../BingeWatch.LoadTest/README.md))
  - **TMDb'ye bağımlı değil.** `CatalogSeeder` kataloğu doğrudan `BingeWatchDb_E2E`
    veritabanına yazıyor: `TmdbStatus="Ended"` + taze `LastSyncedAt` olan bir satırı
    `ShowCatalogService` bayat saymadığı için TMDb'ye hiç gidilmiyor. Böylece süit
    kişisel API anahtarına, ağa ve dışarıda değişen veriye bağlı olmuyor —
    "Breaking Bad kaç sezon" sorusunun cevabı testin kontrolünde
  - **Ayrı veritabanı** — testler kayıt/puanlama satırı yazacak; `BingeOnDb`'ye
    karışırsa elle bakılan veriyle test verisi ayırt edilemez
  - `AppFixture` API ve Web süreçlerini 5074/5182'de kaldırıyor (elle çalıştırılan
    5054/5162 ile çakışmasın), `/health` bekliyor, her test kendi tarayıcı
    bağlamında koşuyor
  - **Playwright tarayıcısı ayrıca kurulmalı:** `BingeWatch.E2E/bin/.../playwright.ps1
    install chromium`. Kurulmadan süitin tamamı düşer
  - **CI'da koşuyor ve gerçek koşuda doğrulandı** — PR
    [#12](https://github.com/oguzozshn/BingeWatch/pull/12): `e2e` işi Linux
    koşucusunda `Passed: 17`, `build-and-test` 148 birim testi. `ci.yml`'e ayrı
    `e2e` işi eklendi. Süit hiçbir sırra
    bağlı değil: kataloğu kendi tohumluyor ve JWT ayarlarını kendi veriyor
    (öncesinde geliştiricinin user-secrets deposuna gizliden bağlıydı, CI'da
    API hiç açılmazdı). LocalDB Windows'a özgü olduğu için Linux koşucuda SQL
    Server servis konteyneri kullanılıyor; bağlantı dizesi
    `BINGEWATCH_E2E_CONNECTION` ile veriliyor. Sunucular `dotnet run` ile
    kalktığı için CI'da Debug derleniyor — Release derlemek çift iş olurdu
  - **Girişli akışlar** — `AppFixture` kayıt formunu doldurarak iki hesap açıyor
    (`PrimaryUser`, `SecondaryUser`) ve oturum çerezini bağlama enjekte ediyor.
    Her test yeniden giriş yapsaydı giriş uçlarının **IP başına 10/5dk** kotası
    dolardı. Kullanıcı adları çalıştırma başına benzersiz — veritabanı kalıcı,
    sabit ad ikinci koşuda "zaten var" derdi
  - ⚠️ **`InteractiveServer` sayfalarında prerender edilen HTML, SignalR devresi
    bağlanana kadar ölü** — o aralıktaki tıklama ve tuş vuruşları sessizce
    kayboluyor. `WaitForInteractiveTabsAsync` devre tepki verene kadar bekliyor.
    Bu beklemeden yazılan testler "buton çalışmıyor" diye yanlış rapor veriyor
  - Devre çökerse tarayıcı konsolu yalnızca "unhandled exception on the current
    circuit" diyor; asıl yığın izi sunucuda. `AppFixture.WebLogTail()` bunu
    hata mesajına ekliyor — bu olmadan teşhis imkânsıza yakındı
  - ⚠️ **Bu bekleme her `InteractiveServer` sayfası için gerekli, yalnızca dizi
    sayfası için değil.** Engelleme testi profil sayfasında aynı tuzağa düştü ve
    ilk yazımında şansla geçti; `main`'e merge edilince kırmızıya döndü. Ayrıca
    Playwright'ın **strict mode**'u: `GetByText` birden çok öğeye takılırsa hata
    veriyor, ama sayfa yarım render edildiği anda tek öğe bulup geçebiliyor —
    yani kararsız. Liste öğesi sayan seçiciye geçildi
  - Yol üstünde dört gerçek hata bulundu, hepsi düzeltildi (bkz. §7.2)
  - **Yük testi** (`BingeWatch.LoadTest`, NBomber) — üç senaryo: dizi detayı
    API'si, liste keşfi, Blazor'ın anonim dizi sayfasını çizmesi. Bilinçli
    olarak CI dışında: yük üreticisi ile uygulama aynı makinede olduğu için
    mutlak sayılar donanıma bağlı, eşik koymak gürültü olur. Okunan şey göreli
    — ilk ölçümde Blazor'ın sayfayı çizmesi aynı veriyi veren API çağrısının
    kabaca iki katı (p95 17.9 ms / 9.3 ms)
  - ⚠️ **Genel istek tavanı yük testini imkânsız kılıyordu** (240 jeton +
    120/dk, sabit kodlu): birkaç yüz istekten sonra ölçülen şey uygulama değil
    jeton kovası oluyordu. Yalnızca *genel* tavan yapılandırılabilir yapıldı
    (`RateLimiting:GlobalTokenLimit`, `RateLimiting:GlobalTokensPerMinute`),
    varsayılanlar eski değerlerle aynı. Güvenlikle ilgili politikalar (giriş
    denemesi, bildirim, yazma) bilerek sabit bırakıldı — onları gevşetmenin
    meşru bir sebebi yok

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

### Faz 7 — Kullanım kolaylığı

- [x] **"Sezonu temizle" butonu** — sezon başlığında, o sezondaki tüm izleme
      işaretlerini kaldırır.
  - **Sorun:** "buraya kadar izledim" tek tıkla onlarca bölüm işaretliyor ama
      geri alma yolu yoktu; yanlış tıklayan kullanıcı hepsini tek tek temizlemek
      zorunda kalıyordu. Toplu işaretleme vardı, toplu geri alma yoktu — asimetri
  - **Arka uç zaten hazırdı:** `POST /api/shows/{id}/seasons/{n}/watched`
      gövdesinde `{ watched: false }` kabul ediyor ve birim testi de vardı. Web
      bu ucu yalnızca `true` ile çağırıyormuş; eksik olan tek şey butondu
  - **Yalnızca işaretli bölüm varsa gösteriliyor** — hiç işaret yokken buton
      ölü arayüz olurdu. Test bunu ayrıca doğruluyor
  - **Onay adımı yok.** Bu düğmenin varlık sebebi yanlış tıklamayı telafi etmek;
      telafi yoluna sürtünme eklemek kendi amacını yer. Ters işlem ("Sezonu
      izledim") zaten tek tık uzakta. Bunun yerine buton sönük duruyor ve
      yalnızca üzerine gelince kırmızıya dönüyor
  - **İşaretlemeden bir farkı var:** "Sezonu izledim" yayınlanmamış bölümleri
      atlıyor, temizleme atlamıyor. Bir şekilde işaretlenmiş yayınlanmamış bir
      bölüm kalırsa kullanıcı ona arayüzden erişemez ve temizleyemezdi
  - ⚠️ **Kaybolan tek şey `WatchedAt` damgaları.** Arayüzde görünmüyor ama
      istatistik sayfasındaki yıllara göre dağılım grafiği bunları kullanıyor;
      yeniden işaretleme bugünün tarihini basar. Küçük ama sessiz bir kayıp
  - Yan etkiler zaten doğru çalışıyordu: dizi durumu "Bitirdim"den geri düşüyor
      ve akış olayı siliniyor (yanlış tıklama takipçilerin akışında kalmıyor)

- [x] **Bölüm tartışmaları** — bölümün kendi sayfasında yorum ipliği;
      **yalnızca o bölümü izlemiş olana açık.**
  - **Bölüm sayfası** `/show/{tmdbId}/season/{n}/episode/{m}` — ilk tasarımda
      iplik dizi sayfasındaki bölüm satırının altında açılıyordu; iki sebeple
      taşındı. Tartışma dar bir satıra sığmıyordu ve bir tartışmanın
      **paylaşılabilir bir adresi** olması gerekiyordu. Rota sezon/bölüm
      numarasından kuruluyor, yerel bölüm id'sinden değil: id katalog yeniden
      tohumlanınca değişir, "S1B1" değişmez
  - **Bölüm satırı yeniden yapılandırıldı:** başlık artık sayfaya giden bir
      `<a>`, işaretleme ayrı bir onay kutusu. Eskiden başlık onay kutusunun
      `<label>`ıydı — yani gezinme ile eylem tek hedefti. Faz 6.3'te watchlist
      kartında düzeltilen kalıbın aynısı
  - **Ayrı uç** (`GET /api/shows/{id}/season/{n}/episode/{m}`) — bölüm özetini
      `ShowDetailDto`'ya eklemek dizi sayfasının yükünü 62 bölümlük bir dizide
      ~20 KB şişiriyordu ve o veri orada hiç kullanılmıyor. Uç tek yanıtta
      bölüm detayını, kullanıcının işaretini ve puanını, üst kırıntıları ve
      sezon sınırını geçen komşu bölümleri veriyor
  - **Meta açıklamasına bölüm özeti konmuyor** — TMDb özetleri olay anlatıyor
      ve arama sonucunda spoiler olarak görünürdü. Sayfanın kendisi özeti
      gösteriyor; oraya kullanıcı ne okuyacağını bilerek geliyor
  - **§3'ün kararını çiğnemiyor, kapsamını daraltıyor.** §3 bölüm bazlı *yazılı
      incelemeyi* reddetmişti; gerekçe "bugün ne izledim" akışının spoiler
      çöplüğüne dönmesiydi. Buradaki iplik **hiçbir akışa düşmüyor**: ne
      `ActivityEvent` ne `Notification` yazılıyor, `/reviews` akışında da yok.
      Kullanıcı yorumları görmeye kendi gidiyor — yayın değil, varış noktası.
      Bu yüzden özgün itiraz geçerli değil (test: `AddAsync_WritesNoActivityOrNotification`)
  - **Spoiler koruması bayrağa değil veriye dayanıyor.** İncelemelerdeki
      `HasSpoilers` kutucuğu burada anlamsız: bir bölümün altındaki yorum zaten
      tanımı gereği o bölümü konuşur. Onun yerine izleme takibi kapı olarak
      kullanılıyor — koruma kullanıcının dürüstlüğüne değil kendi ilerlemesine
      bağlı. Letterboxd ve Reddit'in yapısal olarak yapamadığı şey bu: ikisi de
      okuyucunun nerede olduğunu bilmiyor
  - **Kapı bölüm başına, dizi başına değil.** S1E1'i izlemek S1E2'nin ipliğini
      açmıyor; spoiler tam olarak orada duruyor
  - **Kapı hem okumada hem yazmada, ve sunucuda.** Kilitli iplik yorumları
      *göndermiyor*, arayüzde gizlemiyor. Yorum sayısı da verilmiyor: "burada
      40 yorum var" bilgisi tek başına bölüm hakkında bir şey söyler
      (tartışılan bir olay olmuş)
  - **Kilit 401 değil, kilitli iplik.** Anonim ve izlememiş kullanıcı 200 alıp
      `Locked = true` görüyor; arayüz "neden kapalı" mesajını ancak böyle
      gösterebilir. Üç ayrı mesaj var: anonim, izlememiş, yayınlanmamış
  - **Yan fayda:** yorum yazmak için bölümü işaretlemiş olmak gerekiyor. İzleme
      takibini işaretlemek için gerçek bir sebep doğuyor — ürünün belkemiği olan
      davranış besleniyor
  - **İşareti kaldıran kullanıcının yorumu silinmiyor**, yalnızca ipliği
      kapanıyor. Silmek sessiz veri kaybı olurdu; yerelde de doğrulandı (yorum
      özgün zaman damgasıyla geri geldi)
  - **Silme yetkisi yalnızca yazarda.** İnceleme yorumundaki "inceleme sahibi de
      silebilir" kuralının karşılığı yok: bölümün sahibi diye biri yok.
      Moderasyon `ReportTargetType.EpisodeComment` ile bağlandı — moderatör
      kuyrukta spoiler kapısını aşıyor, bildirilen yorumu okumadan karar veremez
  - Engelleme, yazma kotası (30/dk) ve bildirim altyapısı olduğu gibi devraldı;
      yeni olan tek şey varlık, servis ve panel
  - **Yerelde uçtan uca doğrulandı** (28.07): migration gerçek LocalDB'ye
      uygulandı, anonim → kilitli, girişli+izlememiş → kilitli, işaretleyince
      açıldı, yorum yazıldı, işaret kaldırılınca kapandı ve yorum korundu,
      akışta yorum olayı çıkmadı. API ve tarayıcı konsolunda hata yok
  - **İki hesapla da doğrulandı:** ikinci kullanıcı bölümü izlemeden ipliği
      göremiyor, işaretleyince birincinin yorumunu görüyor ve kendi yorumu
      olmadığı için "Sil" yerine "Bildir" çıkıyor
  - **E2E kapsaması eklendi** (2 test): başlık bağlantısı bölüm sayfasına
      götürüyor; iplik izlemeden kilitli, işaretleyince açılıyor, yorum
      yenilemeden sonra da duruyor, işaret kaldırılınca kapanıyor ama yorum
      silinmiyor
    - Bölüm sayfasına dizi sayfasından **bağlantıyla** geliniyor: Blazor'ın
      gelişmiş gezinmesi devreyi koruduğu için sayfa açılırken zaten
      etkileşimli. Doğrudan `GotoAsync` ile gelinseydi yeni devre kurulana
      kadar tıklamalar yutulurdu (§7.2'deki tuzak)
    - "Başkasının yorumunu görme" bilerek E2E'ye alınmadı: engelleme testi iki
      test hesabını kalıcı olarak engelliyor ve xUnit sırayı garanti etmiyor,
      test kararsız olurdu. O senaryo birim testinde ve elle doğrulandı
  - ⚠️ **Bölüm satırının yapısı değişince E2E kırıldı** — `MarkingEpisode_
      PersistsAcrossReload` kalkan `.episode-checkbox` sınıfını arıyordu. PR
      #19'un E2E'si geçmişti çünkü o koşu satır içi panel sürümündeydi; hata
      ancak sayfa taşındıktan sonraki koşuda çıktı

- [x] **Yeniden izleme (rewatch)** — bölüm sayfasında "↻ Yeniden izledim";
      ilerleme modeline hiç dokunmaz. Gerekçeler ve ürün kararları §7.6'da.

- [x] **"Nerede kaldım" işareti** — yarıda bırakılan bölümde kalınan dakika;
      ana sayfadaki panel "başla" yerine "devam et" der.
  - **Sorun:** ürün bölümü ikili görüyordu — izlendi ya da izlenmedi. Bir
      bölümü yarıda bırakan kullanıcının bunu kaydedecek yeri yoktu; ertesi
      gün "kaçıncı dakikaya kadar izlemiştim" sorusunun cevabı üründe değil
      kullanıcının hafızasındaydı
  - **Ayrı varlık** (`EpisodeBookmark`), `WatchedEpisode`'a nullable kolon
      değil. İkisi birbirini dışlıyor: bölüm izlendiyse yarıda kalmış olamaz.
      Aynı tabloya konsaydı "izlendi ama 32. dakikada" gibi anlamsız bir satır
      şema düzeyinde mümkün olurdu. Bölüm başına tek satır (tekil indeks):
      "kaldığım yer" bir noktadır, geçmişi tutulmaz
  - **Kapı sunucuda:** izlenmiş bölüme işaret konamıyor, negatif ya da bölüm
      süresini aşan dakika reddediliyor (400). İstemcideki `max` niteliği
      klavyeyle aşılabildiği için asıl doğrulama uçta
  - **İşaretleyince işaret siliniyor** — bölüm bitince "nerede kaldım"
      anlamını yitirir. Toplu işaretlemede araya giren yarım bölümler de
      temizleniyor
  - **Panelde öncelikli:** yarıda kalan bölüm sıradakinin önüne geçiyor ve
      listenin en üstünde duruyor. "Devam et", "yeni bölüme başla"dan daha
      güçlü bir sinyal. Panelin sorgu sayısı üçten dörde çıktı ama dizi
      sayısından bağımsız kaldı (N+1 yok)
  - **Yerelde uçtan uca doğrulandı:** izlenmiş bölümde kutu hiç çıkmıyor,
      izlenmemişte 18. dakika kaydedildi → panel "⏸ 18. dakikadan devam et"
      dedi → bölüm işaretlenince işaret silindi ve panel sıradaki bölüme
      geçti → 49 dakikalık bölüme 999 girilince "Dakika 0 ile 49 arasında
      olmalı" hatası döndü. Konsol ve API logunda hata yok

### Faz 8 — Eksik yüzey ve oyunlaştırma (07.08.2026)

> Bir değerlendirmeden çıktı: **arka uç olgun, ön yüz kanıtlanmamıştı.** Aynı
> gün bulunan üç hatanın (ikon fontu, kültür, kullanıcı aramanın hiç olmaması)
> üçü de Web katmanındaydı ve hiçbiri teste takılmamıştı.

- [x] **Profil düzenleme + gizlilik anahtarı** — `/settings/profile`
  - **`IsPrivate` buraya kadar ölü koddu.** Gizlilik kuralı takip, istatistik,
      liste ve arama servislerinin hepsinde özenle uygulanıyordu ama
      kullanıcının bayrağı açacak bir yeri yoktu: özenle korunan bir kapı,
      ama kolu yok. Kullanıcı görünen adını, bio'sunu ve avatarını da kayıt
      olduktan sonra hiç değiştiremiyordu
  - Doğrulama controller'ın içinde test edilemez halde duracaktı; saf bir
      `ProfileValidator`'a alındı
  - **Avatar adresinde şema beyaz listesi** — `javascript:` ve `data:`
      adresleri `<img src>` üzerinden saldırı yüzeyi açardı
  - Kullanıcı adı ve e-posta bilerek düzenlenemiyor: ikisi de kimlik alanı,
      değiştirilmeleri ayrı akışlar (adres doğrulama, eski bağlantıların
      kırılması) gerektirir
- [x] **Devre yarışı kapatıldı** — prerender edilmiş HTML, SignalR devresi
      bağlanana kadar ölü; o aralıktaki tıklamalar sessizce kayboluyordu.
      E2E süiti bunu `WaitForInteractiveTabsAsync` ile elle bekliyordu ama
      **gerçek kullanıcıya aynı koruma verilmemişti** (§7.2'de "test tuzağı"
      diye kayıtlıydı, aslında ürün hatasıydı). `InteractiveComponentBase`
      ilk gerçek render'ı yakalıyor, kontroller o ana kadar devre dışı
- [x] **Web katmanına test** (`RenderingRegressionTests`) — öğenin varlığını
      değil kullanıcının gördüğü sonucu ölçüyor: kritik varlıklar sunuluyor
      mu, ikon fontu yüklü ve ikonlar yer kaplıyor mu, tarih/sayı Türkçe mi,
      prerender'da butonlar devre dışı mı. **Bekçilik ettiği doğrulandı:**
      ikon CSS bağlantısı kaldırılınca test "ikonlar çizilmiyor" diyerek düştü
- [x] **Mini oyun** (`/game`) — iki dizi, hangisinin TMDb puanı yüksek;
      doğru bildikçe uzayan seri
  - Havuz **yerel katalogdan değil TMDb popüler listesinden**: katalog
      yalnızca dokunulmuş dizileri içeriyor, yeni bir kurulumda (Docker, Pi
      demosu) oyun hiç açılmazdı
  - Az oylu diziler eleniyor — tek 10'luk oy almış bir dizi "10.0" görünüyor
      ve böyle bir çift tahmin değil kura olurdu (keşifteki `vote_count.gte`
      eşiğiyle aynı gerekçe)
  - Puanlar cevaptan önce gösterilmiyor; Blazor Server'da bileşen durumu
      sunucuda yaşadığı için cevap DOM'a da düşmüyor
- [x] **Dizi trivia** (`/trivia`) — poster tahmini, yayın yılı, "hangisi en eski"
  - **Sorular hiçbir yerde saklanmıyor**, katalog verisinden anlık üretiliyor.
      Yeni tablo yok, her dizi için içerik yazmak gerekmiyor, havuz katalog
      büyüdükçe kendiliğinden büyüyor
  - *"Veritabanı şişer mi?" sorusunun cevabı:* konu dışı kaldı. 16 dizilik
      katalog zaten **29.068 bölüm satırı** taşıyor; dizi başına beş soru
      saklansa 80 satır ederdi. Asıl kısıt içerik üretimiydi, disk değil
  - **Çeldiriciler rastgele değil:** yıl sorusunda doğru yılın etrafından
      seçiliyor, aksi halde soru tahmin değil eleme oyunu olurdu
  - Aynı yılda başlayan diziler "hangisi en eski" sorusunu cevapsız
      bırakacağı için o durumda soru yeniden üretiliyor
  - Poster sorusunda `alt` bilerek boş: açıklama yazmak cevabı ele verirdi

### Faz 9 — Hesap hijyeni, canlı bildirim, etiketleme, paylaşım (11.08.2026, **plan**)

> Bir gözden geçirmeden çıktı: fazların hepsi kapalı ama **hesap yüzeyi kayıt /
> giriş / şifre sıfırlamadan ibaret**, Faz 4'ün "canlı push Faz 6'da" sözü
> tutulmadı ve §3'ün özellik setindeki etiketleme satırı hâlâ hiçbir faza
> girmedi. Sıra riskten değere: **9.1 → 9.2 → 9.3 → 9.5 → 9.4.** 9.1
> dışındakiler birbirinden bağımsız, paralel gidilebilir. (Numaralar kimlik,
> sıra değil: 9.5 sonradan eklendi ama sosyal değeri 9.4'ün önünde.)
>
> Aşağıdaki maddelerin **işaretsiz olanları yapılmadı**; bu bölüm diğer
> fazların aksine kararların kaydı değil, karar gerektiren yerlerin listesi.
> ⭑ ile işaretli sorular koda başlamadan cevaplanmalı — bir madde bitince o
> soru cevabıyla birlikte kayda geçiyor (bkz. 9.1'in ilk maddesi).
>
> **Durum (13.08.2026):** şifre değiştirme (9.1) ve profildeki kütüphane
> (9.5) bitti; e-posta doğrulama, hesap silme, canlı bildirim, etiketleme ve
> paylaşım kartları açık.

#### 9.1 Hesap hijyeni (önce bu)

- [x] **Şifre değiştirme** — `/settings/password` + `POST /api/auth/change-password`
      (13.08.2026, PR [#38](https://github.com/oguzozshn/BingeWatch/pull/38))
  - **Mevcut şifre soruluyor.** Oturumu ele geçiren birinin şifreyi değiştirip
    sahibini dışarıda bırakması, ancak bu adımla pahalılaşır. "Mevcut şifre
    yanlış" ile "yeni şifre kurala uymuyor" ayrı mesajlar: ikisini tek mesaja
    katlamak kullanıcıyı hangisini düzelteceğini bilmeden bırakırdı
  - **⭑ Karar: damga doğrulaması.** Token'a `security_stamp` claim'i giriyor,
    API her kimlikli istekte damgayı kullanıcının güncel `SecurityStamp`'iyle
    karşılaştırıyor (`ITokenStampValidator`, `OnTokenValidated` olayında).
    Şifre değişince Identity damgayı yeniliyor, eski damgalı token'lar 401
    alıyor. Token ömrü (7 gün) ve yenileme akışı olduğu gibi kaldı
  - **Damga sorgusu her istek yolunda**, bu yüzden kullanıcı başına 60 sn'lik
    önbellekle okunuyor. Şifreyi değiştiren uç önbelleği kendi düşürüyor: tek
    kopyada iptal **anında**. 60 sn yalnızca çok kopyalı çalıştırmada anlamlı
  - **Damgasız token reddediliyor** — bu özellikten önce üretilenler damga
    taşımıyor ve iptal edilemez; kabul etmek mekanizmayı anlamsız kılardı.
    ⚠️ Bedeli: sürüm geçince herkes bir kez yeniden giriş yapar
  - **Sıfırlama akışı da damgayı düşürüyor** — "şifremi unuttum"dan sonra eski
    oturumların ayakta kalması akışın amacına ters
  - ⚠️ **"Oturum kapandı" değil, "token geçersizleşti".** Web bir BFF: JWT
    tarayıcıya hiç gitmiyor, cookie'nin claim'inde duruyor. Damga düşünce o
    cihazdaki **cookie hâlâ geçerli**, yalnızca API istekleri 401 alıyor —
    sayfa girişli görünüp veri çekemiyor. Arayüz metni bu yüzden "oturumların
    kapatıldı" değil "oturumlar geçersizleşti" diyor. Web'in 401'i görünce
    oturumu kapatması ayrı bir iş; aynı boşluk token süresi dolduğunda da var
    (JWT 7 gün sabit, cookie 7 gün **kayan** — cookie token'dan uzun yaşıyor)
- [ ] **E-posta doğrulama** — kayıt sonrası bağlantı, `ConfirmEmail` ucu
  - **Neden gerçek bir açık:** adres bugün hiç doğrulanmıyor ama şifre sıfırlama
    akışı çalışıyor. Yanlış ya da başkasının adresiyle açılan bir hesabın
    sıfırlama bağlantısı o adrese gider
  - ⭑ **Doğrulanmamış hesap ne yapabilir?** Öneri: girebilsin ama **yazma uçları
    kapalı** olsun (30/dk kotasının olduğu uçlar). Girişi tamamen kapatmak kayıt
    akışını kırar, tamamen açmak spam'i davet eder
  - **Hesap sayımına kapalı kalmalı** — Faz 1'in kuralı burada da geçerli:
    "bu adres zaten kayıtlı" diyen bir ekran üyeleri tek tek saydırır
  - SMTP göndericisi (§7.1) olduğu gibi devralınır; yeni bağımlılık yok
- [ ] **Hesap silme + veri dışa aktarma** — `/settings/account`
  - ⭑ **Sert silme mi, anonimleştirme mi?** Öneri: kullanıcı satırı ve kişisel
    alanlar silinsin, **inceleme ve yorumlar "silinmiş kullanıcı"ya bağlansın**.
    Cascade silme başkalarının ipliklerini ortasından koparır — bölüm
    tartışmaları ve inceleme yorumları tam olarak bunun üstünde duruyor
  - **FK zinciri tek tek denetlenmeli:** `Follow`, `ActivityEvent`,
    `Notification` (hem sahibi hem `ActorId`), `UserBlock`, `UserList`,
    `Report` — `Report.ResolvedById` bir moderatörü işaret ediyor, moderatör
    hesabı silinince kuyruğun geçmişi de gitmemeli
  - **Dışa aktarma** tek JSON: izlemeler, puanlar, incelemeler, listeler,
    yorumlar. Silmeden önce indirmeyi teklif eden bir akış
  - Geri dönüşü yok; onay için kullanıcı adını yazdırmak yeterli sürtünme

#### 9.2 Canlı bildirim

- [ ] **Zil sayacı anlık güncellensin** — bugün yalnızca sayfa yüklenirken okunuyor
  - ⭑ **Önce ölçülecek: ayrı hub gerekli mi?** Blazor Server'ın devresi zaten
    açık. Sunucu tarafında kullanıcı başına bir olay yayını + `InvokeAsync(
    StateHasChanged)` yetiyorsa ikinci bir SignalR bağlantısı kurmanın anlamı
    yok. Ayrı `NotificationHub` ancak bu yetmezse
  - Engelleme kuralı bildirim üretiminde zaten uygulanıyor olmalı
    (`NotificationService`); canlı yol onu **tekrar etmemeli**, aynı kapıdan
    geçmeli — yoksa iki ayrı doğruluk kaynağı olur
  - Tek kopya çalışıyoruz; çok kopyaya çıkılırsa backplane gerekir. Kararı
    yazıp geçmek şimdilik yeterli

#### 9.3 Etiketleme (§3.B — hedef özellik setinin son açık satırı)

- [ ] **`Tag` + `UserShowTag`**, kütüphaneye etiket filtresi
  - **Karar: gizli başlıyor.** §7.6'nın gerekçesi geçerli — gizliyi açmak kolay,
    açığı geri kapatmak kullanıcının verisini görünürlükten geri çekmek demek.
    Açık etiket serbest metin olduğu için moderasyon kuyruğuna da girerdi
  - Gizli başladığı için etiketler **kullanıcıya özel**; paylaşılan sözlük
    kurulmuyor. Ad normalize edilir (kırpma + küçük harf) ki `Comfort` ile
    `comfort` iki ayrı etiket olmasın
  - `/discover` **"Kütüphanem"** moduna filtre — tür filtresi "ve" ile
    çalışıyor, etiket de "ve" olmalı: `comfort-show` + `bitmedi` kesişim demek
  - Ekleme/silme arayüzü dizi sayfasında, `<AddToListButton>` kalıbının yanında

#### 9.4 Paylaşım kartları (§3.C)

- [ ] **Üretilen OG görseli** — bugün `og:image` ham TMDb backdrop'u
  - Sunucuda çizim (SkiaSharp / ImageSharp): poster + dizi adı + puan
  - **Cache şart** — crawler her istekte görüntü çizdiremez; `ETag` + süreli
    cache, kart içeriği değişince anahtar değişir
  - Uç **anonime açık olmalı** (crawler giriş yapmaz). Bu yüzden kişisel kartta
    gizli profil kuralı ayrıca uygulanacak: gizli profilin kartı üretilmez

#### 9.5 Profilde kütüphane — başkasının izledikleri ve izleyecekleri

> 13.08.2026'da eklendi. Soru şuydu: kullanıcılar birbirlerinin izlediği
> dizileri ve izleme listelerini görebilmeli mi? **Cevap: evet.**

- [x] **Profilde `Kütüphane` ve `İzleyecekleri` sekmeleri** — `UserShow`
      üzerinden, dizi seviyesinde (13.08.2026, PR
      [#39](https://github.com/oguzozshn/BingeWatch/pull/39))
  - `GET /api/users/{username}/library` tek yanıt veriyor; sekme değiştirmek
    yeni istek gerektirmiyor. Sıralama listeye eklenme tarihine göre — "son
    izlenen" sırası, bakmamaya karar verilen bölüm verisini okumak olurdu
  - ⚠️ **Yol üstünde bulunan hata:** sayfanın sekmeleri prerender yarışına
    karşı korumasızdı; devre bağlanmadan yapılan tıklama sessizce
    kayboluyordu. E2E ilk koşuda yakaladı, sayfa `InteractiveComponentBase`'e
    bağlandı. Faz 8'de ürün genelinde kapatılan boşluk **yeni sayfada
    tekrarlanmıştı** — kalıp hatırlanmadıkça her yeni etkileşimli sayfada
    geri gelecek
  - **Karar aslında yarı yarıya verilmişti.** Başkasının profili bugün zaten
    favori 4 diziyi, izlenen bölüm / bitirilen dizi sayısını, toplam süreyi ve
    `/@kullanici/stats` sayfasında **"en çok izlenenler"** listesini (dizi adı,
    poster, bölüm sayısı, dakika) gösteriyor. Akış da takip edilenlerin bölüm
    bölüm "izledi" olaylarını taşıyor. Yani "ne izliyor" zaten dışarıda;
    saklanan tek şey kütüphanenin **liste hâli**. Toplamı gösterip kalemleri
    gizlemek kimseyi korumuyor, yalnızca veriyi işe yaramaz kılıyor
  - **İki ayrı sekme, tek liste değil.** "İzledim" geçmiş beyanı, "izleyeceğim"
    niyet beyanı; sosyal değerleri de mahremiyet hisleri de farklı. Asıl fayda
    ikincisinde: birinin listesine bakıp "bunu ben de izleyecektim" demek,
    takip etmenin en somut sebeplerinden biri
  - **Yeni gizlilik kolu yok.** Mevcut `IsPrivate` devralınıyor. Üçüncü bir
    "kütüphanem gizli olsun" anahtarı kullanıcıya aynı soruyu üçüncü kez sormak
    olur ve listelerdeki iki katmanlı kuralla (kapalı liste + gizli profil)
    çelişen bir üçüncü kural doğurur
  - ⚠️ **Bölüm bazlı ilerleme dışarı açılmıyor.** "Breaking Bad izliyor" açık
    olabilir ama "S3B4'te" olması kalmalı: kimseye faydası yok ve Faz 7'deki
    bölüm tartışmalarının kapısı tam olarak bu veriye dayanıyor — dışarıdan
    okunabilir olması, kimin hangi ipliği açabildiğini de okunabilir kılar.
    Dizi seviyesinde durum yeter
  - **Engelleme ve gizlilik süzgeçleri bu uçta da eksiksiz uygulanmalı** —
    Faz 6.1'de listelerde tek tek kurulan kalıbın aynısı. Yeni bir kural değil
    ama unutulursa sessiz bir sızıntı olur

---

## 7. Bilinen Sorunlar / Doğrulanacaklar

### 7.1 Açık sorunlar

✅ **Docker imajları x86-64'te build edildi ve çalıştırıldı (02.08.2026).**
Geliştirme makinesine Docker Desktop kuruldu (WSL2'nin "Sanal Makine Platformu"
özelliği kapalıydı, `wsl --install --no-distribution` ile açıldı). İki imaj da
derlendi, üç servis de `healthy` oldu, migration'lar ilk denemede uygulandı ve
[DEPLOY.md](DEPLOY.md) §4'teki dört riskin hiçbiri gerçekleşmedi. Kayıt, giriş,
TMDb'den veri çekme, bölüm işaretleme ve etkileşimli bileşenler doğrulandı;
konteyner yeniden başlatıldıktan sonra veri korundu.

⚠️ **Yol üstünde gerçek bir hata bulundu: Linux'ta tarih ve sayılar İngilizce
geliyordu.** Arayüzün tamamı Türkçe ama uygulama hiçbir yerde kültür
ayarlamıyordu; Windows'ta OS zaten `tr-TR` olduğu için görünmüyordu, Linux
konteynerde invariant kültüre düşüp "20 January 2008" / "8.5" yazıyordu (ekran
okuyucu etiketleri dahil). **Bu hata Pi kurulumunda da vardı, fark edilmemişti**
— Docker'a özgü değil, Linux'ta çalıştırmanın kendisinden geliyor. `Program.cs`
başında kültür sabitlenerek düzeltildi; ayrıntı [DEPLOY.md](DEPLOY.md) §9.

⚠️ **Faz 6.5'in "kod tarafı yerelde uçtan uca doğrulandı" notu şüpheli.** 28.07'de
görüldü ki API 25.07 16:31'den beri bu makinede hiç açılamıyordu (`Jwt:Key`
eksikti — bkz. Faz 0). Serilog, health check ve migration yeniden denemesi
gerçek bir çalıştırmayla değil, büyük ihtimalle yalnızca okumayla doğrulanmış.
Anahtarlar girildikten sonra `/health` ve `/health/ready` çalıştığı görüldü;
gerisi hâlâ doğrulanmayı bekliyor.

**Yük testinin liste keşfi ölçümü güvenilmez.** İlk ölçüm sırasında
veritabanında hiç liste yoktu; sıralama alt sorgusu ve poster önizleme yolları
hiç çalışmadı, yani `api_liste_kesfi` sayısı olduğundan iyi görünüyor. Anlamlı
bir sayı için önce veri üretilmeli. Diğer iki senaryo gerçek veriyle ölçüldü.

**Şifre sıfırlama artık açık — SMTP yapılandırıldı ve uçtan uca doğrulandı
(30.07.2026).** Ayrı bir Gmail hesabı (`bingewatch.noreply@gmail.com`) açılıp
uygulama şifresi üretildi, beş değer (`Smtp:Host/Port/User/Password/FromAddress`)
bu makinenin user-secrets deposuna girildi. Gerçek bir `/forgot-password`
isteğiyle test edildi: API logunda `Sifre sifirlama e-postasi gonderildi`
satırı görüldü ve mail gerçekten gelen kutusuna ulaştı (spam'e düşmedi).

⚠️ **Bu yapılandırma yalnızca bu makinede.** User-secrets deposu proje
klasörünün dışında (`%APPDATA%\Microsoft\UserSecrets\`) olduğu için OneDrive
ile senkronlanmıyor — yeni bir makineye taşınırken ya da Docker'a geçerken
(§7.1 Docker maddesi) aynı beş değer o ortamda yeniden girilmeli (`.env` ya da
environment variable olarak).

Ayrıntılı anlatım: [DEPLOY.md](DEPLOY.md) §6.

### 7.2 Faz 6.6'da bulunan ve çözülenler

- ✅ **ARIA durum nitelikleri geçersiz yazılıyordu — Faz 6.3'ün ARIA işi sessizce
  etkisizdi.** Blazor bir `bool` nitelik değerini HTML boolean niteliği gibi ele
  alıyor: `true` için niteliği **boş değerle** yazıyor (`aria-selected=""`),
  `false` için **hiç yazmıyor**. ARIA ise birebir `"true"`/`"false"` metnini
  bekler; boş değer geçersiz, niteliğin yokluğu "belirtilmemiş" demek. Beş yer
  etkilenmişti ve hepsi Faz 6.3'ün iddialarının tam merkezindeydi:
  `StarRating`'in `aria-checked`'i (ekran okuyucu **verilen puanı hiç
  bildirmiyordu**), dizi sayfası sekmelerinin `aria-selected`'i, sezon
  akordiyonunun ve WatchList arama kutusunun `aria-expanded`'i. Tek yerden
  [AriaAttribute.Aria](../BingeWatch.Web/AriaAttribute.cs) ile düzeltildi.
  Playwright'ın erişilebilirlik anlık görüntüsü yakaladı — koda bakarak
  görülmesi zor, çünkü kaynak tamamen doğru görünüyor
- ✅ **Ok tuşu sekmede seçimi değiştiriyor ama odağı taşımıyordu.**
  `OnTabKeyDown` yalnızca `activeTab`'i güncelliyordu; odak eski sekmede
  kalıyor, o sekme de aynı anda `tabindex="-1"`e düşüyordu. Sonuç: ekran
  okuyucu yeni sekmeyi duyurmuyor ve odak şeritten kopuyor — "tek durak"
  kuralının amacı bozuluyor. Sunucu tarafı DOM odağını değiştiremediği için
  `bingeWatchFocusElement` JS köprüsü eklendi

- ✅ **Atlama bağlantısına ileri Tab ile ulaşılamıyordu.** `Routes.razor`'daki
  `<FocusOnNavigate Selector="h1">` odağı **ilk yüklemede de** `h1`'e alıyordu.
  Odak `h1`'e oturunca DOM'da ondan önce gelen her şey — Faz 6.3'te eklenen
  "İçeriğe atla" bağlantısı **ve navbar'ın tamamı** — ileri Tab ile erişilemez
  hale geliyordu; menüye ancak Shift+Tab ile ulaşılabiliyordu. Yani Faz 6.3'ün
  eklediği atlama bağlantısı hiç çalışmamış. Ölçüm: sayfa yüklenince
  `document.activeElement` = `h1`, ilk Tab → `select.sort-select` (içerik ortası).
  `FocusOnNavigate` kaldırıldı, yerine
  [navigation-focus.js](../BingeWatch.Web/wwwroot/js/navigation-focus.js): Blazor'ın
  `enhancedload` olayına bağlanıyor, bu olay ilk yüklemede tetiklenmediği için
  odak belgenin başında kalıyor; gezinmede ise `h1`'e taşınıyor.
  **Tuzak:** `enhancedload` son DOM yamasından önce tetiklenebiliyor — odaklanan
  `h1` düğümü sonradan değişince odak `body`'ye düşüyordu; odaklama kısa bir
  pencerede (0/50/200 ms) tekrarlanıyor
- ✅ **§7.1'in "rolsüz `[Authorize]` sayfaları yönlendirmiyor" maddesi yanlıştı.**
  Ölçüldü: `/notifications`, `/feed`, `/watchlist`, `/settings/blocks` ve
  `/admin/reports` **hepsi 302 ile `/login`'e gidiyor** (cookie auth
  middleware'i, `ReturnUrl` parametresiyle) ve gövde boş dönüyor. Madde
  kaldırıldı, yerine yönlendirmeyi doğrulayan `[Theory]` testi kondu
- ✅ **Eski proje adı kullanıcıya görünen yerlerde kalmıştı** — navbar markası
  `BingeOn.Web`, Swagger başlığı `BingeOn API`. Kod tanımlayıcıları
  (`BingeOnDbContext`, `BingeOnDb` veritabanı adı) bilerek değiştirilmedi:
  migration ve bağlantı dizesi zinciri açılıyor, kazancı yok
- ℹ️ **Yan sonuç: `NoIndex` meta etiketi crawler'a hiç ulaşmıyor.** `NoIndex="true"`
  verilen sayfaların hepsi `[Authorize]` arkasında ve anonim istek 302 alıyor;
  gövde üretilmediği için `<meta name="robots">` de yazılmıyor. Zararsız ama
  ölü — asıl koruma yönlendirmenin kendisi. Faz 6.4'ün bu kararı fazlalık

### 7.3 Faz 6.3'te çözülenler

- ✅ **WatchList arama blur yarışı** (eski §7.1): "Ara" butonuna tıklamak input'u
  blur ediyor, `OnSearchBlur`'ün 200 ms'lik zamanlayıcısı sonuçlarla yarışıyordu.
  Öneri listesine `@onmousedown:preventDefault` eklendi — tıklama artık odağı
  input'tan almıyor.
- ✅ **WatchList'teki `Console.WriteLine` çağrıları** (Faz 0'dan kalan, 6 adet)
  `ILogger`'a taşındı.

### 7.4 Faz 3'te çözülenler

- ✅ **Sezonlar katlanamıyor**: ShowView'daki sezonlar artık akordiyon; varsayılan kapalı,
  ilk yarım kalmış sezon açık başlar.

### 7.5 Faz 2'de çözülenler

- ✅ **Kalp butonu** (eski §7.1): kök neden doğrulandı — Web `SeriesDto.FirstAirDate` `string`,
  API `DateTime?` bekliyordu ve `ShowYear` çıplak yıl (`"2008"`) gönderiyordu. ShowView'ın
  katalog API'sine taşınmasıyla kökten çözüldü, tarayıcıda doğrulandı.
- ✅ **Durum takılması**: tüm bölümler izlendikten sonra bir bölümün işareti kaldırılınca
  dizi sonsuza dek "Bitirdim"de kalıyordu.
- ✅ **TMDb boş tarih → arama çökmesi**: TMDb, tarihi bilinmeyen yapımlar için
  `first_air_date` alanını `""` döndürüyor; tek bozuk kayıt tüm arama isteğini 500 ile
  düşürüyordu. `NullableDateTimeConverter` eklendi.
- ✅ **N+1 `external_ids` çağrıları** (§2.3) ve **`WatchListItem` → `UserShow` migration'ı**.

### 7.6 Tamamlanmamış / ertelenen maddeler

**Faz 1'de ertelenip sonradan yapılanlar**

- ✅ Şifre sıfırlama akışı — 28.07.2026'da eklendi, SMTP göndericisi dahil.
  Kodda eksik bir şey yok; yalnızca sağlayıcı hesabı açılıp beş değer
  girilmeyi bekliyor. Adımlar §7.1'de

**§3'teki hedef özellik setinde olup hiçbir faza girmemiş maddeler**

*28.07.2026'da fark edildi: fazların tamamı bitti ama §3'ün özellik
tablolarındaki iki satır hiçbir faz maddesine dönüşmemiş. Yani "fazlar bitti"
ile "hedef özellik seti bitti" aynı şey değil.*

**İkisi de 28.07.2026'da bilinçli olarak ertelendi**; rewatch 30.07.2026'da
yapıldı, etiketleme hâlâ açık. Aşağıdaki değerlendirmeler o gün yapıldı;
devam edilirken sıfırdan çıkarmaya gerek yok.

#### Etiketleme (§3.B — `comfort-show`, `bırakılan` gibi kendi tag'leri)

**Hiç yapılmadı.** Ne varlık, ne servis, ne arayüz; §4'teki veri modelinde de yok.
Tek izi §3'teki tablo satırı.

- **Neden değerli:** Dizileri sınıflandırmanın tek yolu şu an `Status` — sabit ve
  tek boyutlu. Etiket, kullanıcının kütüphanesini kendi kafasına göre
  dilimlemesini sağlar
- **Maliyet: orta.** Yeni varlık (`Tag` + `UserShowTag`), ekleme/silme arayüzü,
  keşfin "Kütüphanem" moduna etiket filtresi. Mevcut hiçbir modeli bozmuyor —
  katmanlı bir ekleme
- **Önce cevaplanacak soru: etiketler gizli mi, herkese açık mı?** Gizliyse
  kişisel bir düzenleme aracı. Açıksa sosyal keşif katmanı olur ama serbest
  metin olduğu için moderasyon gerekir. Gizli başlayıp sonra açmak kolay,
  tersi zor

#### Yeniden izleme / rewatch (§3.A) — ✅ 30.07.2026'da yapıldı

Bölüm bazlı model seçildi (aşağıdaki "daha ucuz üçüncü yol" değil): `RewatchNo`
kolonu artık amacına uygun kullanılıyor, her yeniden izleme ayrı satır.

**Çözüm, ilerleme modelini kırmamak üzerine kuruldu.** Endişe şuydu: ürünün
ilerleme modeli tek yönlü tek geçiş varsayıyor. Anahtar karar, mevcut
`RewatchNo == 0` filtrelerinin **tamamını olduğu gibi bırakmak** oldu — böylece
durum geçişleri, "Sırada ne var" ve ilerleme çubuğu rewatch'ı hiç görmüyor.
Rewatch yalnızca yeni satır ekliyor; `UpdateShowStatusAsync` bile çağrılmıyor.

Ürün sorularının cevapları:

- **Dizi durumu "Bitirdim"de kalır.** Rewatch durum geçişi tetiklemez
- **"Sırada ne var" yalnızca ilk geçişi izler** — panelin işi yeni içerik
  önermek; bitmiş bir dizi rewatch yüzünden oraya geri dönmez
- **İlerleme çubuğu değişmez** (%100'de kalır) — aynı sebeple
- **İstatistikte süre rewatch'ları sayar.** Kural tek cümle: *izleme aktivitesi
  ölçen her şey (süre, yıllık dağılım, en çok izlenenler) rewatch'ı sayar;
  yalnızca "kaç farklı bölüm izledin" tekildir.* "En çok izlenenler"in comfort
  show'larla dolması hata değil, doğru yansıma

⚠️ **Yol üstünde bulunan hata:** bir bölümün işareti kaldırılınca
`ApplyWatchedAsync` yalnızca `RewatchNo == 0` satırını siliyordu. Rewatch
satırları yetim kalıp istatistikte süre saymaya devam ederdi — izlenmemiş
görünen bir bölüm zaman harcatıyor olurdu. Artık işaret kaldırma tüm geçişleri
siliyor (`UnmarkingEpisode_AlsoRemovesRewatches`).

- **Arayüz bölüm sayfasında**, yalnızca bölüm izlenmişse görünür: "↻ Yeniden
  izledim" + sayaç + "Son kaydı geri al". İşaretlenmemiş bölümde buton ölü
  arayüz olurdu (Faz 7'deki "sezonu temizle" ile aynı gerekçe)
- **Uçlar:** `POST`/`DELETE /api/shows/{id}/episodes/{episodeId}/rewatch`.
  İlk izleme yoksa `POST` 400 döner — kapı sunucuda
- **Yerelde uçtan uca doğrulandı:** işaretle → 2 rewatch ekle → istatistikte
  "1 izlenen bölüm + 2 yeniden izleme", süre 59×3 = 2 sa 57 dk → geri al →
  1'e düştü → işareti kaldır → rewatch'lar da silindi, istatistik sıfırlandı.
  Konsol ve API logunda hata yok

💡 *Değerlendirme sırasında düşünülen ucuz alternatif (dizi/sezon seviyesinde
salt sayaç) uygulanmadı: bölüm bazlı kayıt tarih bilgisini de koruduğu ve
ilerleme modeline dokunmadan yapılabildiği görüldüğü için gereksiz kaldı.*

---

## 8. Riskler

- **TMDb rate limit** (~50 req/s) — cache ve senkron servis olmadan bölüm bazlı takip ölçeklenmez
- **Spoiler yönetimi** diziye özgü ve zor bir UX problemi; Faz 3'te baştan doğru modellenmeli, sonradan eklenmesi zor
- **`WatchListItem` migration'ı** tek seferlik ve geri dönüşü zahmetli — Faz 1'de yedekle
