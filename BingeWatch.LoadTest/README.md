# Yük testi

Elle çalıştırılan bir teşhis aracı. **CI'ın parçası değil ve olmamalı:** yük
üreticisi ile uygulama aynı makinede koştuğu için mutlak sayılar donanıma ve o
anki yüke bağlı; eşik koyup CI'ı kırmızıya düşürmek gürültüden ibaret olur.

Anlamlı olan taraf **göreli**: hangi uç diğerlerinden kat kat yavaş, yük artınca
p95 nerede kopuyor, bir değişiklikten sonra aynı uç yavaşladı mı. Raporları
(`load-reports/`) karşılaştırma için sakla.

## Çalıştırma

Genel istek tavanı varsayılan olarak dakikada 120. Bu tavanın içinden anlamlı
ölçüm yapılamaz — birkaç yüz istekten sonra ölçülen şey uygulama değil, jeton
kovası olur. API'yi tavanı gevşeterek başlat:

```powershell
$env:RateLimiting__GlobalTokenLimit    = '1000000'
$env:RateLimiting__GlobalTokensPerMinute = '1000000'
dotnet run --project BingeWatch.API --urls http://localhost:5054
```

Web'i ayrı bir kabukta:

```powershell
$env:Api__BaseUrl = 'http://localhost:5054/'
dotnet run --project BingeWatch.Web --urls http://localhost:5162
```

Sonra:

```powershell
dotnet run --project BingeWatch.LoadTest -- --api http://localhost:5054 --web http://localhost:5162 --show 1396
```

`--show` katalogda **bulunan** bir TMDb dizi kimliği olmalı; yoksa senaryo
TMDb'ye gider ve ölçüm ağ gecikmesine karışır.

## Senaryolar

| Senaryo | Ne ölçüyor |
|---|---|
| `api_dizi_detay` | Sezon ve bölümleriyle tek dizi — dizi sayfasının en ağır API çağrısı |
| `api_liste_kesfi` | İmleçli liste keşfi; sıralama alt sorgusu ve poster önizlemesi |
| `web_dizi_sayfasi` | Blazor Server'ın anonim dizi sayfasını baştan çizmesi — SEO trafiğinin yolu |

## İlk ölçüm (28.07.2026, geliştirme makinesi)

Boş bir E2E veritabanına karşı, 30 saniye, sıfır hata:

| Senaryo | p50 | p95 | p99 |
|---|---|---|---|
| `api_dizi_detay` | 6.3 ms | 9.3 ms | 13.2 ms |
| `api_liste_kesfi` | 3.0 ms | 6.8 ms | 10.0 ms |
| `web_dizi_sayfasi` | 12.6 ms | 17.9 ms | 22.1 ms |

⚠️ **`api_liste_kesfi` sayısı olduğundan iyi görünüyor:** ölçüm sırasında
veritabanında hiç liste yoktu, yani sıralama ve poster önizleme yolları hiç
çalışmadı. Anlamlı bir sayı için önce veri üretilmeli.

Okunacak tek şey oran: Blazor'ın sayfayı çizmesi, aynı veriyi veren API
çağrısının kabaca iki katı. Bu beklenen bir şey, ama SEO trafiği bu yoldan
geldiği için bir regresyonda ilk bakılacak yer burası.
