# BingeWatch.API — çok aşamalı build.
# Bağlam (context) solution kökü olmalı:
#   docker build -f BingeWatch.API/Dockerfile -t bingewatch-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Önce yalnızca proje dosyaları: kaynak değiştiğinde restore katmanı
# yeniden çalışmasın diye ayrı katmanda duruyor.
COPY BingeWatch.sln .
COPY BingeWatch.API/BingeWatch.API.csproj BingeWatch.API/
COPY BingeWatch.Web/BingeWatch.Web.csproj BingeWatch.Web/
COPY BingeWatch.Tests/BingeWatch.Tests.csproj BingeWatch.Tests/
RUN dotnet restore BingeWatch.API/BingeWatch.API.csproj

COPY . .
# --no-restore burada da yok: bkz. BingeWatch.Web/Dockerfile. API'de Blazor
# yok ama aynı tuzak statik varlıkları sessizce düşürebiliyor; iki imajın
# aynı sırayı izlemesi tutarlı olsun diye.
RUN dotnet publish BingeWatch.API/BingeWatch.API.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl runtime imajında hazır gelmiyor; compose'daki healthcheck buna bağlı.
# Kurulmazsa yoklama her seferinde başarısız olur ve "api hazır" koşuluna
# bağlı olan web servisi hiç ayağa kalkmaz.
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Log dizini henüz root'ken açılıp sahipliği veriliyor. Serilog'un dosya
# sink'i buraya yazıyor ama konteyner "app" kullanıcısıyla çalışıyor ve /app
# root'a ait; dizin hazır olmazsa yazamaz. Serilog sink hatalarını yuttuğu
# için uygulama sorunsuz görünür, yalnızca paneldeki log görüntüleyici boş
# kalır — sessiz kırılma.
RUN mkdir -p /app/logs && chown app:app /app/logs

# Root olmayan kullanıcı: aspnet imajında hazır gelen "app" kullanıcısı.
USER app

# aspnet imajının varsayılanı 8080.
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "BingeWatch.API.dll"]
