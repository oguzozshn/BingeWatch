using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace BingeWatch.API.Configurations
{
    /// <summary>
    /// Politika adları. Uçlar bunlara <c>[EnableRateLimiting]</c> ile bağlanır;
    /// politika seçmeyen uçlar yalnızca global tavana tabidir.
    /// </summary>
    public static class RateLimitPolicies
    {
        /// <summary>Giriş/kayıt — parola deneme hızını IP başına kısar.</summary>
        public const string Auth = "auth";

        /// <summary>Bildirim oluşturma — moderasyon kuyruğunu spam'den korur.</summary>
        public const string Report = "report";

        /// <summary>Yazma uçları — kullanıcı başına (anonimde IP başına).</summary>
        public const string Write = "write";
    }

    public static class RateLimitingSetup
    {
        /// <summary>
        /// Kotalar kullanıcı başına sayılır, kimlik yoksa IP'ye düşülür. Aynı ağdaki
        /// kullanıcıların birbirinin kotasını yemesi ancak anonim isteklerde mümkün
        /// olsun diye kimlik önce geliyor.
        /// </summary>
        /// <param name="configuration">
        /// Yalnızca genel tavan yapılandırmadan okunuyor
        /// (<c>RateLimiting:GlobalTokenLimit</c> ve
        /// <c>RateLimiting:GlobalTokensPerMinute</c>). Sebebi somut: yük testi bu
        /// tavanın içinden anlamlı ölçüm yapamıyor — birkaç yüz istekten sonra
        /// ölçülen şey uygulama değil, jeton kovası oluyor. Varsayılanlar eski
        /// sabit değerlerle aynı, yani üretim davranışı değişmiyor.
        /// <para>
        /// Güvenlikle ilgili politikalar (giriş denemesi, bildirim, yazma)
        /// bilerek sabit bırakıldı: onları gevşetmenin meşru bir sebebi yok.
        /// </para>
        /// </param>
        public static IServiceCollection AddBingeWatchRateLimiting(
            this IServiceCollection services, IConfiguration? configuration = null)
        {
            var tokenLimit = configuration?.GetValue("RateLimiting:GlobalTokenLimit", 240) ?? 240;
            var tokensPerPeriod = configuration?.GetValue("RateLimiting:GlobalTokensPerMinute", 120) ?? 120;

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                // İstemci ne zaman tekrar deneyebileceğini bilmeli; aksi halde 429
                // gören arayüz sıkı bir yeniden deneme döngüsüne giriyor.
                options.OnRejected = async (context, cancellationToken) =>
                {
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        context.HttpContext.Response.Headers.RetryAfter =
                            ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
                    }

                    // İçerik tipi WriteAsJsonAsync'e verilmeli; Response.ContentType'a
                    // önceden yazmak işe yaramıyor, metot kendi tipini geçiriyor.
                    await context.HttpContext.Response.WriteAsJsonAsync(new
                    {
                        type = "https://tools.ietf.org/html/rfc9110#section-15.5.29",
                        title = "Çok fazla istek",
                        status = StatusCodes.Status429TooManyRequests,
                        detail = "Kısa sürede çok fazla istek gönderdin; biraz bekleyip tekrar dene."
                    }, options: null, contentType: "application/problem+json", cancellationToken);
                };

                // Genel tavan: tek bir istemci API'yi doldurmasın. Blazor Server tarafı
                // sayfa başına birkaç istek attığı için pencere dakikalık ve geniş.
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetTokenBucketLimiter(PartitionKey(context), _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = tokenLimit,
                        TokensPerPeriod = tokensPerPeriod,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

                // Parola denemesi: dar pencere, kuyruk yok. Identity'nin kendi lockout'u
                // tek hesabı korur; bu politika hesap taramasını yavaşlatır.
                options.AddPolicy(RateLimitPolicies.Auth, context =>
                    RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(5),
                        QueueLimit = 0
                    }));

                options.AddPolicy(RateLimitPolicies.Report, context =>
                    RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0
                    }));

                options.AddPolicy(RateLimitPolicies.Write, context =>
                    RateLimitPartition.GetTokenBucketLimiter(PartitionKey(context), _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 60,
                        TokensPerPeriod = 30,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            });

            return services;
        }

        /// <summary>Kimliği olan kullanıcı kendi kotasını, anonim istek IP kotasını harcar.</summary>
        private static string PartitionKey(HttpContext context)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return string.IsNullOrEmpty(userId) ? $"ip:{ClientIp(context)}" : $"user:{userId}";
        }

        private static string ClientIp(HttpContext context) =>
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
