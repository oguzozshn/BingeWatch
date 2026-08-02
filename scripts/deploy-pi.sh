#!/usr/bin/env bash
#
# Pi 5 (arm64) üzerindeki demo kurulumunu güncel tutar.
#
#   ./scripts/deploy-pi.sh          # main'i çek, değişiklik varsa yeniden kur
#   ./scripts/deploy-pi.sh --force  # değişiklik olmasa da yeniden kur
#
# Veritabanına dokunmaz: `mssql-data` volume'ü yerinde kalır ve migration'lar
# API açılışında uygulanır (Database:MigrateOnStartup). Yani şema kodla birlikte
# ilerler, veri kaybolmaz. PC'deki ve Pi'deki veritabanları bilerek ayrıdır —
# Pi bir demo, aynı veriyi taşımaya çalışmıyoruz.
#
# ⚠️ .env dosyası git'e girmez; Pi'de bir kez elle doldurulur ve burada
# korunur. Bu script ona hiç dokunmaz.

set -euo pipefail

cd "$(dirname "$0")/.."

FORCE=${1:-}
COMPOSE=(docker compose -f docker-compose.yml -f docker-compose.pi.yml)

if [[ ! -f .env ]]; then
    echo "HATA: .env yok. Once 'cp .env.example .env' ve degerleri doldur." >&2
    exit 1
fi

echo "==> Guncellemeler cekiliyor"
BEFORE=$(git rev-parse HEAD)
git pull --ff-only
AFTER=$(git rev-parse HEAD)

if [[ "$BEFORE" == "$AFTER" && "$FORCE" != "--force" ]]; then
    echo "Zaten guncel ($(git rev-parse --short HEAD)). Yeniden kurmak icin: --force"
    exit 0
fi

echo "==> Imajlar derleniyor (Pi'de 15-30 dk surebilir)"
"${COMPOSE[@]}" up -d --build

echo "==> Servisler bekleniyor"
for _ in $(seq 1 60); do
    if curl -sf http://localhost:5054/health/ready >/dev/null 2>&1; then
        break
    fi
    sleep 5
done

"${COMPOSE[@]}" ps

# Sessiz kırılma testi: bu dizin yoksa sayfalar gelir ama hiçbir şey tıklanmaz
# (bkz. DEPLOY.md §8). Yayın çıktısı eksik demektir, uyarmadan geçmeyelim.
if ! "${COMPOSE[@]}" exec -T web ls wwwroot/_framework >/dev/null 2>&1; then
    echo "UYARI: wwwroot/_framework yok — Blazor etkilesimi calismayacak (DEPLOY.md §8)" >&2
fi

echo "==> $(git rev-parse --short HEAD) yayinda"
