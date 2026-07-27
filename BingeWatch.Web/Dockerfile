# BingeWatch.Web — çok aşamalı build.
# Bağlam (context) solution kökü olmalı:
#   docker build -f BingeWatch.Web/Dockerfile -t bingewatch-web .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY BingeWatch.sln .
COPY BingeWatch.API/BingeWatch.API.csproj BingeWatch.API/
COPY BingeWatch.Web/BingeWatch.Web.csproj BingeWatch.Web/
COPY BingeWatch.Tests/BingeWatch.Tests.csproj BingeWatch.Tests/
RUN dotnet restore BingeWatch.Web/BingeWatch.Web.csproj

COPY . .
RUN dotnet publish BingeWatch.Web/BingeWatch.Web.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Bkz. API Dockerfile'ı: healthcheck curl'e bağlı, imajda hazır gelmiyor.
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

USER app

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

# TLS ters vekilde sonlanıyor; konteyner içinde HTTPS'e yönlendirmek
# var olmayan bir porta yönlendirmek olurdu (bkz. Program.cs).
ENV EnableHttpsRedirection=false

ENTRYPOINT ["dotnet", "BingeWatch.Web.dll"]
