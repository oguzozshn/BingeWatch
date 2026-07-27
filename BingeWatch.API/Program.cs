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
using BingeWatch.API.Services;
using BingeWatch.API.Data;
using BingeWatch.API.Models;

namespace BingeWatch.API
{
    public class Program
    {
        public static void Main(string[] args)
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
            builder.Services.AddScoped<ITokenService, TokenService>();
            builder.Services.AddHostedService<TmdbSyncService>();

            // ASP.NET Core Identity
            builder.Services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 6;
            })
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

            app.MapControllers();

            app.Run();
        }
    }
}
