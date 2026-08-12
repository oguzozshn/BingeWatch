using BingeWatch.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BingeWatch.API.Services
{
    /// <summary>
    /// Damga kontrolü her kimlikli isteğin yolunda; bu yüzden veritabanına her
    /// seferinde gidilmiyor, kullanıcı başına kısa ömürlü bir önbellek tutuluyor.
    /// Süre bilinçli olarak kısa: iptalin ne kadar gecikebileceğinin üst sınırı bu.
    /// </summary>
    public class TokenStampValidator : ITokenStampValidator
    {
        /// <summary>Token'daki damga claim'inin adı.</summary>
        public const string ClaimType = "security_stamp";

        /// <summary>
        /// Tek kopya çalışırken iptal anında oluyor (<see cref="Invalidate"/>
        /// önbelleği düşürüyor). Bu süre yalnızca birden çok kopya çalıştığında
        /// devreye girer: damgayı değiştiren kopya diğerlerinin önbelleğini
        /// düşüremez, onlar en geç bu kadar sonra öğrenir.
        /// </summary>
        public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

        private readonly BingeOnDbContext _context;
        private readonly IMemoryCache _cache;

        public TokenStampValidator(BingeOnDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<bool> IsCurrentAsync(string userId, string? stamp)
        {
            // Damgasız token bu özellikten önce üretilmiş olabilir ya da elle
            // kurcalanmış olabilir; ikisinde de iptal edilemez, ikisi de reddedilir.
            if (string.IsNullOrEmpty(stamp))
                return false;

            var current = await _cache.GetOrCreateAsync(CacheKey(userId), entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                return _context.Users
                    .Where(u => u.Id == userId)
                    .Select(u => u.SecurityStamp)
                    .FirstOrDefaultAsync();
            });

            // Kullanıcı silinmişse damga da yok; token geçerli sayılmamalı.
            return current != null && current == stamp;
        }

        public void Invalidate(string userId) => _cache.Remove(CacheKey(userId));

        private static string CacheKey(string userId) => $"security-stamp:{userId}";
    }
}
