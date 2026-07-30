# Çalıştırma ve Deploy

İki yol var: **yerel geliştirme** (LocalDB, değişmedi) ve **Docker Compose**
(gerçek SQL Server). Faz 6.5'te ikincisi eklendi.

---

## 1. Yerel geliştirme

Faz 6.5 öncesiyle aynı; hiçbir adım değişmedi.

```bash
dotnet run --project BingeWatch.API
```

Ayrı bir terminalde:

```bash
dotnet run --project BingeWatch.Web
```

API `http://localhost:5054`, Web `http://localhost:5162` üzerinde. Veritabanı
LocalDB, migration'lar açılışta uygulanıyor.

Gizli anahtarlar User Secrets'tan okunuyor:

```bash
dotnet user-secrets --project BingeWatch.API set "Tmdb:ApiKey" "..."
```

> **Not:** Web artık API adresini `Api:BaseUrl` yapılandırmasından okuyor
> (`appsettings.json`'da varsayılan `http://localhost:5054/`). Önceden
> `Program.cs` içinde sabitti ve konteynerde çalışması imkânsızdı.

---

## 6. Şifre sıfırlama e-postası (SMTP)

Şifre sıfırlama akışı hazır ama **teslimat yapılandırılmadan kapalı**: SMTP
tanımlı değilse `/api/auth/forgot-password` **503** döner ve Web "şu an
kullanılamıyor" der. Uygulamanın geri kalanı bundan etkilenmez.

Seçim ortama değil **yapılandırmaya** bakıyor:

| Durum | Devreye giren |
|---|---|
| `Smtp:Host` ve `Smtp:FromAddress` dolu | Gerçek gönderim (`SmtpPasswordResetNotifier`) |
| Boş **ve** Development | Bağlantı loga yazılır (`LoggingPasswordResetNotifier`) |
| Boş **ve** Production | Özellik kapalı, uç 503 (`DisabledPasswordResetNotifier`) |

Development'ta da SMTP tanımlanabilir; kurulumu üretime çıkmadan denemenin yolu
budur.

> ⚠️ Loga yazan uygulama **yalnızca Development içindir**. Sıfırlama bağlantısı
> hesabın parolasını değiştirmeye yeten bir sırdır; üretimde loga düşmesi, logu
> okuyabilen herkese hesapları açmak demek olurdu.

### Gmail ile kurulum (domain gerekmez)

Kendi domainin yoksa en hızlı yol. Günlük ~500 mail sınırı var.

1. Google hesabında **iki adımlı doğrulamayı aç** — uygulama şifresi ancak bundan
   sonra üretilebiliyor.
2. Google Hesabı → Güvenlik → **Uygulama şifreleri**'nden yeni bir şifre üret.
   16 haneli, boşluklu görünen bir dizi verir.
3. Bu şifreyi yapılandırmaya yaz. **Hesap parolan değil**, üretilen bu şifre:

```bash
dotnet user-secrets --project BingeWatch.API set "Smtp:Host" "smtp.gmail.com"
dotnet user-secrets --project BingeWatch.API set "Smtp:Port" "587"
dotnet user-secrets --project BingeWatch.API set "Smtp:User" "sen@gmail.com"
dotnet user-secrets --project BingeWatch.API set "Smtp:Password" "abcdefghijklmnop"
dotnet user-secrets --project BingeWatch.API set "Smtp:FromAddress" "sen@gmail.com"
```

Docker'da aynı değerler `.env` üzerinden (`SMTP_HOST`, `SMTP_USER`, …).

⚠️ **Gmail'de gönderen adres, kimlik doğrulanan adresle aynı olmalı.** Farklı bir
`FromAddress` verirsen Gmail gönderimi reddeder.

⚠️ **Teslim edilebilirlik.** Gmail'den giden "şifreni sıfırla" mailleri — içinde
bağlantı olduğu için — alıcının spam klasörüne düşebilir. Şifre sıfırlamada bu
can sıkıcı: kullanıcı maili hiç görmez ve özelliği bozuk sanır. Kendi domainin
olduğunda Resend/Brevo gibi bir servise geçmek bu riski büyük ölçüde kaldırır;
**kod değişmez, yalnızca bu beş değer değişir.**

### Yerelde gerçek mail göndermeden denemek

Sahte bir SMTP sunucusu (Papercut, smtp4dev, MailHog) çalıştırıp:

```bash
dotnet user-secrets --project BingeWatch.API set "Smtp:Host" "localhost"
dotnet user-secrets --project BingeWatch.API set "Smtp:Port" "1025"
dotnet user-secrets --project BingeWatch.API set "Smtp:FromAddress" "bingewatch@yerel.test"
dotnet user-secrets --project BingeWatch.API set "Smtp:UseTls" "false"
```

Bu sunucuların çoğu TLS konuşmaz; `Smtp:UseTls=false` bunun için var.

### Doğrulama

1. `/forgot-password` sayfasından kayıtlı bir e-posta gönder.
2. Yanıt her durumda aynıdır ("kayıtlı bir hesap varsa gönderildi") — hesap
   sayımına kapalı olduğu için başarı/başarısızlık buradan anlaşılmaz.
3. **Gerçek sonuç API logunda:** başarıda `Sifre sifirlama e-postasi gonderildi`,
   hatada `gonderilemedi` satırı ve istisna. Gönderim hatası bilinçli olarak
   kullanıcıya yansıtılmıyor; yansısaydı "500 = hesap var, 200 = yok" gibi bir
   sızıntı olurdu.

---

## 2. Docker Compose

> Bu kurulum gerçek bir Docker üzerinde (Raspberry Pi 5, arm64) baştan sona
> çalıştırıldı: imajlar derlendi, migration'lar uygulandı, uygulama tünel
> arkasından kullanıldı. arm64'e özgü farklar ve o sırada çıkan sorunlar
> §7'de. Aşağıdaki "Bilinen riskler" listesi x86-64 için hâlâ sınanmadı.

### Hazırlık

```bash
cp .env.example .env
```

`.env` dosyasını doldur:

| Değişken | Not |
|---|---|
| `MSSQL_SA_PASSWORD` | SQL Server en az 8 karakter ve karmaşıklık istiyor; zayıf parolada konteyner sessizce ölür |
| `JWT_KEY` | HMAC-SHA256 için en az 32 karakter (`openssl rand -base64 48`) |
| `TMDB_API_KEY` | TMDb v4 bearer token |
| `ADMIN_USERNAME` | Moderasyon paneline erişecek kullanıcı; **önce uygulamadan kaydolmalı**, rol bir sonraki açılışta atanır |

### Çalıştırma

```bash
docker compose up -d --build
```

Web `http://localhost:8080`, API yalnızca `127.0.0.1:5054` üzerinde (dışarıya
kapalı). SQL Server da yalnızca localhost'a bağlı.

### Servisler

| Servis | Görev | Bağımlılık |
|---|---|---|
| `db` | SQL Server 2022, veri `mssql-data` volume'ünde kalıcı | — |
| `api` | Katalog + kullanıcı katmanı, migration'ları uygular | `db` sağlıklı olana kadar bekler |
| `web` | Blazor Server | `api` sağlıklı olana kadar bekler |

### Health check'ler

| Uç | Anlamı |
|---|---|
| `GET /health` | **Liveness** — süreç ayakta mı? Hiçbir bağımlılığa bakmaz |
| `GET /health/ready` | **Readiness** — SQL Server'a gerçekten ulaşılıyor mu? |

Ayrım bilinçli: veritabanı düştüğünde konteyneri yeniden başlatmak sorunu
çözmez, sadece yeniden başlatma döngüsüne sokar. Orchestrator'ın "öldür ve
yeniden başlat" kararı `/health`'e, "trafik gönder" kararı `/health/ready`'ye
bakmalı. İkisi de rate limiting'in dışında.

---

## 3. Migration stratejisi

Varsayılan olarak API açılışta migration uyguluyor
(`Database:MigrateOnStartup`). Konteynerde SQL Server ile API aynı anda ayağa
kalktığı için ilk denemeler bağlantı hatasıyla düşebiliyor; sınırlı sayıda
yeniden deneniyor (`Database:MigrateRetryCount`, varsayılan 10 × 5 sn).

**Birden çok API kopyası çalıştıracaksan** bunu `false` yap: aynı anda migrate
etmeye çalışan iki kopya çakışır. O kurulumda migration ayrı bir deploy adımı
olmalı:

```bash
dotnet ef database update --project BingeWatch.API
```

---

## 4. Bilinen riskler (ilk çalıştırmada bakılacak yerler)

- **`curl` kurulumu.** Health check'ler `curl`'e bağlı ve .NET runtime imajında
  hazır gelmiyor; Dockerfile'larda `apt-get install curl` var. Taban imaj
  değişirse bu satır da gözden geçirilmeli — curl yoksa yoklama hep başarısız
  olur ve `api`'ye bağlı olan `web` hiç ayağa kalkmaz.
- **SQL Server healthcheck yolu.** `sqlcmd` 2022 imajında
  `/opt/mssql-tools18/bin/` altında ve kendinden imzalı sertifika yüzünden `-C`
  gerekiyor. İmaj etiketi değişirse yol da değişebilir.
- **Blazor Server + birden çok kopya.** SignalR devresi sunucuya yapışık;
  `web`'i ölçeklersen ters vekilde sticky session gerekir.
- **TLS.** Compose HTTP konuşuyor; TLS'in ters vekilde (nginx/Caddy/Traefik)
  sonlanması bekleniyor. `web` konteynerinde `EnableHttpsRedirection=false` bu
  yüzden — konteyner içinde HTTPS'e yönlendirmek var olmayan bir porta
  yönlendirmek olurdu.

---

## 5. Loglama

Her iki uygulama da Serilog kullanıyor, loglar stdout'a yazılıyor
(`docker compose logs -f api`). İstek başına tek satır düşüyor
(`UseSerilogRequestLogging`); framework'ün üç satırlık kendi logu ve EF'in her
SQL'i basması `appsettings.json`'daki `Serilog:MinimumLevel:Override` ile
kapatıldı.

> `Serilog:MinimumLevel:Override` altındaki **her anahtar** bir logger kaynağı
> olarak okunuyor. Oraya açıklama amaçlı `_comment` gibi bir anahtar koyarsan
> uygulama açılışta çöker.

---

## 7. Raspberry Pi 5 (arm64)

Gerçek bir Pi 5 üzerinde çalıştırıldı ve doğrulandı. Ana compose dosyası x86-64
varsayıyor; farkları `docker-compose.pi.yml` kapatıyor:

```bash
docker compose -f docker-compose.yml -f docker-compose.pi.yml up -d --build
```

Pi'de ilk build 15–30 dakika sürüyor (iki .NET SDK derlemesi). Çok yavaş
gelirse imajları başka bir makinede `docker buildx build --platform linux/arm64`
ile üretip bir registry üzerinden taşımak da mümkün.

### Donanım ve sistem

| Gereksinim | Neden |
|---|---|
| 64-bit OS (`uname -m` → `aarch64`) | .NET 10 imajları 32-bit ARM'ı desteklemiyor |
| 8GB model | SQL Edge tek başına ~1.5–2GB istiyor |
| NVMe ya da iyi bir SSD | SQL Server ailesi SD kartta çok yavaş |
| Swap ≥ 2GB | Yetmezse derleme OOM ile ölür (`/etc/dphys-swapfile`) |

### ⚠️ 4KB sayfa boyutlu çekirdek şart

Pi 5'in varsayılan çekirdeği **16KB** bellek sayfası kullanıyor. SQL Edge'in
bellek ayırıcısı (jemalloc) 4KB varsayıyor ve açılışta ölüyor:

```
<jemalloc>: Unsupported system page size
Out of memory allocating bitmask: Cannot allocate memory
```

Mesaj yanıltıcı — RAM'le ilgisi yok. Kontrol ve çözüm:

```bash
getconf PAGESIZE                      # 16384 ise sorun bu
echo "kernel=kernel8.img" | sudo tee -a /boot/firmware/config.txt
sudo reboot                           # sonrasında 4096 dönmeli
```

`kernel8.img` özel derlenmiş bir çekirdek değil, Raspberry Pi OS'un kutudan
çıkan ikinci çekirdeği — diğer Pi modellerinde zaten varsayılan. Kaybedilen şey
Pi 5'e özel 16KB sayfa optimizasyonu; ölçülebilir ama küçük. Geri almak için
satırı silmek yeterli. **Yedek alarak ekle:** açılış bozulursa SSH ile
düzeltemezsin, kartı çıkarıp `bootfs` bölümünü başka bir makineden açman gerekir.

### Veritabanı: neden SQL Edge

`mcr.microsoft.com/mssql/server` **amd64-only**; Pi'de `no matching manifest`
ile düşer. Azure SQL Edge arm64 destekliyor ve migration'lar olduğu gibi
uygulanıyor — kodda ve şemada değişiklik gerekmedi.

Microsoft ürünü emekliye ayırdı; imaj çekilebiliyor ama kalıcı kurulum için
doğru cevap değil. Uzun vadeli yol PostgreSQL'e geçmek: Pi'de birinci sınıf
vatandaş, çok daha az RAM yiyor ve çekirdek numarası gerektirmiyor. Maliyeti
`Npgsql` provider'a geçiş + migration'ların yeniden üretilmesi.

Bir de imaj farkı var: **SQL Edge `sqlcmd` içermiyor.** Ana dosyadaki yoklama bu
yüzden burada hep başarısız olur; override yerine portun açılmasına bakıyor.
Bu "hazır" garantisi vermez ama API'nin migration retry'ı (10 × 5 sn) farkı
kapatıyor.

### Dışarıya açmak: Cloudflare Tunnel

Router'da port açmadan, sabit IP ya da DDNS olmadan HTTPS adres verir. Blazor
Server'ın SignalR devresi tünel üzerinden sorunsuz kuruluyor (WebSocket dahil,
doğrulandı).

```bash
cloudflared tunnel --url http://localhost:8080
```

Verdiği `*.trycloudflare.com` adresi **geçici**: süreç kapanınca ölür, yeniden
açılınca değişir. `tmux` içinde çalıştır ve `Ctrl+B` → `D` ile ayrıl (`Ctrl+C`
tüneli öldürür). Kalıcı adres için Cloudflare'de bir domain gerekiyor; o zaman
adlandırılmış tünel + `cloudflared service install` ile Pi yeniden başlasa da
adres sabit kalır.

Web bu kurulumda yalnızca `127.0.0.1`'e bağlı — tünel Pi'nin kendi üstünde
çalıştığı için servisi ev ağına açmaya gerek yok.

### Takılma noktaları

| Belirti | Sebep |
|---|---|
| `no matching manifest for linux/arm64` | `-f docker-compose.pi.yml` verilmemiş |
| `db` sürekli yeniden başlıyor, logda `jemalloc` | 16KB sayfalı çekirdek (yukarı bak) |
| `db` düşüyor, logda `Password validation failed` | `MSSQL_SA_PASSWORD` karmaşıklık kuralını geçmiyor |
| `web` için `address already in use` | Override'da `ports: !override` etiketi eksik; port listeleri birleşip kendisiyle çakışıyor |
| Build ortasında süreç ölüyor | Swap yetersiz |
| Sayfalar geliyor ama hiçbir şey tıklanmıyor | `_framework/blazor.web.js` 404 — bkz. §8 |

---

## 8. Sessiz kırılma: Blazor'un framework varlıkları

Yayınlanmış Web uygulamasında `GET /_framework/blazor.web.js` **404** dönerse
site çalışıyor *görünür* — sayfalar sunucuda render edilip geldiği için menü,
formlar ve içerik gelir — ama devre hiç kurulmadığından etkileşimli hiçbir
bileşen çalışmaz. Blazor'un hata kutusu da gizli kaldığı için ekranda uyarı
çıkmaz.

Sebebi Dockerfile'daki katman optimizasyonuydu: yalnızca `.csproj` dosyaları
ortamdayken `restore` edip ardından `dotnet publish --no-restore` çalıştırmak,
`microsoft.aspnetcore.app.internal.assets` paketinden gelen framework
varlıklarını yayın çıktısından düşürüyor — `wwwroot/_framework` hiç oluşmuyor ve
`staticwebassets.endpoints.json` manifestine kayıt girmiyor.

**Bayrak iki Dockerfile'dan da kaldırıldı; geri koymayın.** Restore katmanı
yerinde: paketler önbellekte olduğu için `publish`'in kendi restore'u hızlı
geçiyor.

Şüphelenirsen:

```bash
docker compose exec web ls wwwroot/_framework
```

Dizin yoksa yayın çıktısı eksik demektir.
