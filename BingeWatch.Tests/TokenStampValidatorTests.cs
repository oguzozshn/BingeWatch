using BingeWatch.API.Data;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BingeWatch.Tests
{
    /// <summary>
    /// Token iptalinin tek mekanizması; kırılırsa "şifremi değiştirdim" hiçbir
    /// oturumu düşürmez ve bunu kimse fark etmez.
    /// </summary>
    public class TokenStampValidatorTests
    {
        private static BingeOnDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<BingeOnDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new BingeOnDbContext(options);
        }

        private static async Task<AppUser> SeedUserAsync(BingeOnDbContext context, string stamp)
        {
            var user = new AppUser
            {
                Id = "user1",
                UserName = "ali",
                DisplayName = "Ali",
                SecurityStamp = stamp,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        private static TokenStampValidator CreateValidator(BingeOnDbContext context, IMemoryCache cache) =>
            new(context, cache);

        [Fact]
        public async Task IsCurrentAsync_AcceptsMatchingStamp()
        {
            using var context = CreateContext();
            using var cache = new MemoryCache(new MemoryCacheOptions());
            await SeedUserAsync(context, "STAMP-1");

            Assert.True(await CreateValidator(context, cache).IsCurrentAsync("user1", "STAMP-1"));
        }

        [Fact]
        public async Task IsCurrentAsync_RejectsStaleStampAfterInvalidate()
        {
            using var context = CreateContext();
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var user = await SeedUserAsync(context, "STAMP-1");
            var validator = CreateValidator(context, cache);

            // Önce geçerli: değer önbelleğe de giriyor.
            Assert.True(await validator.IsCurrentAsync("user1", "STAMP-1"));

            // Şifre değişimi: Identity damgayı yeniler, uç da önbelleği düşürür.
            user.SecurityStamp = "STAMP-2";
            await context.SaveChangesAsync();
            validator.Invalidate("user1");

            Assert.False(await validator.IsCurrentAsync("user1", "STAMP-1"));
            Assert.True(await validator.IsCurrentAsync("user1", "STAMP-2"));
        }

        /// <summary>
        /// Kabul edilen davranışın kaydı: önbellek düşürülmezse eski damga
        /// önbellek ömrü kadar geçerli kalır. Tek kopyada uç bunu hemen
        /// düşürüyor; çok kopyada diğerleri en geç o kadar sonra öğrenir.
        /// </summary>
        [Fact]
        public async Task IsCurrentAsync_ServesCachedStampUntilInvalidated()
        {
            using var context = CreateContext();
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var user = await SeedUserAsync(context, "STAMP-1");
            var validator = CreateValidator(context, cache);

            Assert.True(await validator.IsCurrentAsync("user1", "STAMP-1"));

            user.SecurityStamp = "STAMP-2";
            await context.SaveChangesAsync();

            Assert.True(await validator.IsCurrentAsync("user1", "STAMP-1"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task IsCurrentAsync_RejectsTokenWithoutStamp(string? stamp)
        {
            using var context = CreateContext();
            using var cache = new MemoryCache(new MemoryCacheOptions());
            await SeedUserAsync(context, "STAMP-1");

            // Bu özellikten önce üretilmiş token'lar damgasız; iptal edilemeyen
            // bir token'a güvenmek mekanizmayı anlamsız kılardı.
            Assert.False(await CreateValidator(context, cache).IsCurrentAsync("user1", stamp));
        }

        [Fact]
        public async Task IsCurrentAsync_RejectsUnknownUser()
        {
            using var context = CreateContext();
            using var cache = new MemoryCache(new MemoryCacheOptions());
            await SeedUserAsync(context, "STAMP-1");

            Assert.False(await CreateValidator(context, cache).IsCurrentAsync("silinmis-kullanici", "STAMP-1"));
        }

        /// <summary>
        /// Damga token'a gerçekten yazılıyor mu? Yazılmazsa doğrulayıcı her
        /// isteği reddederdi ve hata ancak çalıştırmada görünürdü.
        /// </summary>
        [Fact]
        public void CreateToken_CarriesSecurityStampClaim()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "bu-test-anahtari-en-az-256-bit-olmali-yoksa-imza-atilamaz",
                    ["Jwt:Issuer"] = "bingewatch-test",
                    ["Jwt:Audience"] = "bingewatch-test"
                })
                .Build();

            var user = new AppUser
            {
                Id = "user1",
                UserName = "ali",
                DisplayName = "Ali",
                SecurityStamp = "STAMP-1"
            };

            var token = new TokenService(configuration).CreateToken(user, Array.Empty<string>());

            var parsed = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);
            Assert.Equal("STAMP-1",
                parsed.Claims.FirstOrDefault(c => c.Type == TokenStampValidator.ClaimType)?.Value);
        }
    }
}
