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

namespace BingeWatch.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Database bağlantısı
            builder.Services.AddDbContext<BingeOnDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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
            builder.Services.AddBingeWatchRateLimiting();

            // Hataları RFC 7807 (ProblemDetails) formatında döndür
            builder.Services.AddProblemDetails();

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

            // Database migration'ları uygula
            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<BingeOnDbContext>();
                context.Database.Migrate();

                await SeedAdminsAsync(scope.ServiceProvider, app.Configuration);
            }

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

            app.MapControllers();

            await app.RunAsync();
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
