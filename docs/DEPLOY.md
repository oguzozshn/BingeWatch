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

## 2. Docker Compose

> ⚠️ **Bu kurulum henüz gerçek bir Docker üzerinde çalıştırılmadı.** Dosyalar
> yazıldı ve compose şeması doğrulandı, ama `docker build` / `docker compose up`
> denenmedi (geliştirme makinesinde Docker kurulu değil). İlk çalıştırmada
> düzeltme gerekebilir; en olası noktalar aşağıda "Bilinen riskler"de.

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
