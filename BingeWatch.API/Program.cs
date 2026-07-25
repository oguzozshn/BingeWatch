using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using BingeWatch.API.Configurations;
using BingeWatch.API.Clients;
using BingeWatch.API.Services;
using BingeWatch.API.Data;

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
            builder.Services.Configure<TmdbSettings>(builder.Configuration.GetSection("Tmdb"));
            builder.Services.AddHttpClient<TmdbClient>();
            builder.Services.AddScoped<ITmdbService, TmdbService>();
            builder.Services.AddScoped<IWatchListService, WatchListService>();

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

            app.MapControllers();

            app.Run();
        }
    }
}
