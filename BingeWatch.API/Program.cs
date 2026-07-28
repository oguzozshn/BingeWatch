using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using BingeWatch.API.Configurations;
using BingeWatch.API.Clients;
using Microsoft.Extensions.Logging;
using BingeWatch.API.Services;
using BingeWatch.API.Data;
using BingeWatch.API.Models;
using Serilog;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace BingeWatch.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Serilog — yapılandırma appsettings'ten okunur. Konteynerde loglar
            // stdout'a yazılır (docker logs); dosya sink'i yalnızca yerelde açılıyor.
            builder.Host.UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console());

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection yapılandırılmamış.");

            // Database bağlantısı. Konteynerde SQL Server ile API aynı anda ayağa
            // kalkıyor; geçici bağlantı hataları yeniden denenmeli.
            builder.Services.AddDbContext<BingeOnDbContext>(options =>
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null)));

            // TMDB servislerini kaydet
            builder.Services.AddMemoryCache();
            builder.Services.Configure<TmdbSettings>(builder.Configuration.GetSection("Tmdb"));
            builder.Services.AddHttpClient<TmdbClient>();
            builder.Services.AddScoped<ITmdbService, TmdbService>();
            builder.Services.AddScoped<IWatchListService, WatchListService>();
            builder.Services.AddScoped<IShowCatalogService, ShowCatalogService>();
            builder.Services.AddScoped<IEpisodeProgressService, EpisodeProgressService>();
            builder.Services.AddScoped<IRatingService, RatingService>();
            builder.Services.AddScoped<IReviewService, ReviewService>();
            builder.Services.AddScoped<IFollowService, FollowService>();
            builder.Services.AddScoped<IActivityService, ActivityService>();
            builder.Services.AddScoped<INotificationService, NotificationService>();
            builder.Services.AddScoped<IReviewInteractionService, ReviewInteractionService>();
            builder.Services.AddScoped<IUserStatsService, UserStatsService>();
            builder.Services.AddScoped<IUserListService, UserListService>();
            builder.Services.AddScoped<IDiscoverService, DiscoverService>();
            builder.Services.AddScoped<IBlockService, BlockService>();
            builder.Services.AddScoped<IReportService, ReportService>();
            builder.Services.AddScoped<ITokenService, TokenService>();
            builder.Services.AddHostedService<TmdbSyncService>();

            // ASP.NET Core Identity
            builder.Services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 6;
            })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<BingeOnDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();

            // JWT Bearer authentication
            var jwtKey = builder.Configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("Jwt:Key is not configured. Set it via user-secrets or environment variables.");

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                    };
                });

            builder.Services.AddAuthorization();

            // İstek kotaları — politikalar Configurations/RateLimitPolicies.cs'te
            builder.Services.AddBingeWatchRateLimiting(builder.Configuration);

            // Hataları RFC 7807 (ProblemDetails) formatında döndür
            builder.Services.AddProblemDetails();

            // Health check'ler. "live" yalnızca sürecin ayakta olduğunu söyler
            // (orchestrator'ın yeniden başlatma kararı buna bakar); "ready" veritabanına
            // gerçekten ulaşılıp ulaşılmadığını sınar — trafiği almaya hazır mıyız?
            builder.Services.AddHealthChecks()
                .AddSqlServer(
                    connectionString,
                    name: "sqlserver",
                    failureStatus: HealthStatus.Unhealthy,
                    tags: new[] { "ready" });

            // Swagger hizmetleri
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "BingeOn API",
                    Version = "v1"
                });
            });

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                await MigrateAsync(scope.ServiceProvider, app.Configuration);
                await SeedAdminsAsync(scope.ServiceProvider, app.Configuration);
            }

            // İstek logu: her istek için tek satır (yol, durum, süre). ASP.NET'in
            // varsayılan üç satırlık logu yerine, konteyner çıktısı okunabilir kalsın.
            app.UseSerilogRequestLogging();

            // Yakalanmamış hataları ProblemDetails olarak döndür
            app.UseExceptionHandler();

            // Swagger middleware
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "BingeOn API v1");
                });
            }

            // app.UseHttpsRedirection(); // İsteğe bağlı

            app.UseAuthentication();
            app.UseAuthorization();

            // Kota, kimlikten sonra: politikalar kullanıcı başına bölümleniyor.
            app.UseRateLimiter();

            // Health uçları kotanın dışında: orchestrator'ın yoklaması 429 yememeli.
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                // Liveness: hiçbir bağımlılığa bakmaz. Veritabanı düşünce konteynerin
                // yeniden başlatılması sorunu çözmez, sadece döngüye sokar.
                Predicate = _ => false
            }).DisableRateLimiting();

            app.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready"),
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            }).DisableRateLimiting();

            app.MapControllers();

            await app.RunAsync();
        }

        /// <summary>
        /// Migration'ları uygular. Konteynerde SQL Server ile API aynı anda ayağa
        /// kalktığı için ilk denemeler bağlantı hatasıyla düşebiliyor; sınırlı sayıda
        /// yeniden deneniyor. <c>Database:MigrateOnStartup</c> false ise hiç çalışmaz —
        /// birden çok kopya aynı anda migrate etmeye kalkarsa çakışır, o kurulumda
        /// migration ayrı bir deploy adımı olmalı.
        /// </summary>
        private static async Task MigrateAsync(IServiceProvider services, IConfiguration configuration)
        {
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(Program));

            if (!configuration.GetValue("Database:MigrateOnStartup", true))
            {
                logger.LogInformation("Database:MigrateOnStartup kapalı; migration atlandı.");
                return;
            }

            var retries = Math.Max(configuration.GetValue("Database:MigrateRetryCount", 10), 1);
            var delay = TimeSpan.FromSeconds(
                Math.Max(configuration.GetValue("Database:MigrateRetryDelaySeconds", 5), 1));

            var context = services.GetRequiredService<BingeOnDbContext>();

            for (var attempt = 1; attempt <= retries; attempt++)
            {
                try
                {
                    await context.Database.MigrateAsync();
                    logger.LogInformation("Migration'lar uygulandı ({Attempt}. denemede).", attempt);
                    return;
                }
                catch (Exception ex) when (attempt < retries)
                {
                    logger.LogWarning(ex,
                        "Veritabanına ulaşılamadı ({Attempt}/{Retries}); {Delay} sn sonra tekrar denenecek.",
                        attempt, retries, delay.TotalSeconds);
                    await Task.Delay(delay);
                }
            }

            // Son deneme de patlarsa hata yukarı çıksın: veritabanısız açılan bir API
            // her isteği 500'le karşılar, sessizce ayakta kalması işe yaramaz.
            await context.Database.MigrateAsync();
        }

        /// <summary>
        /// Moderatörleri yapılandırmadan okuyup rolü atar (<c>Admin:Usernames</c>).
        /// Rol vermenin uygulama içinde bir yolu bilerek yok: paneli açacak kişi
        /// deploy'u yapan kişi olsun, panelden panel yetkisi dağıtılamasın.
        /// </summary>
        private static async Task SeedAdminsAsync(IServiceProvider services, IConfiguration configuration)
        {
            var usernames = configuration.GetSection("Admin:Usernames").Get<string[]>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

            if (!await roleManager.RoleExistsAsync(AppRoles.Admin))
                await roleManager.CreateAsync(new IdentityRole(AppRoles.Admin));

            if (usernames == null || usernames.Length == 0)
                return;

            var userManager = services.GetRequiredService<UserManager<AppUser>>();
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(Program));

            foreach (var username in usernames.Where(u => !string.IsNullOrWhiteSpace(u)))
            {
                var user = await userManager.FindByNameAsync(username);
                if (user == null)
                {
                    // Henüz kaydolmamış olabilir; bir sonraki açılışta tekrar denenir.
                    logger.LogWarning("Admin olarak tanımlı {Username} kullanıcısı bulunamadı.", username);
                    continue;
                }

                if (!await userManager.IsInRoleAsync(user, AppRoles.Admin))
                    await userManager.AddToRoleAsync(user, AppRoles.Admin);
            }
        }
    }
}
