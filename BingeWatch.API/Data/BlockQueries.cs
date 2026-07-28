using Microsoft.EntityFrameworkCore;

namespace BingeWatch.API.Data
{
    /// <summary>
    /// Engel filtrelerinin ortak yardımcıları. Engel iki yönlü etki ettiği için
    /// her okuma yolunun aynı soruyu sorması gerekir: "bu iki kullanıcı arasında
    /// hangi yönde olursa olsun engel var mı?"
    /// </summary>
    public static class BlockQueries
    {
        /// <summary>
        /// İsteği yapanın göremeyeceği kullanıcı id'leri — hem engellediği hem
        /// kendisini engelleyenler. Anonim istekte boş liste döner.
        /// </summary>
        /// <remarks>
        /// Sorgu içinde alt sorgu yerine listeyi çekip <c>Contains</c> kullanıyoruz:
        /// engel listesi kullanıcı başına küçük, alt sorgu ise her okuma yolunda
        /// tekrarlanan bir join maliyeti demek.
        /// </remarks>
        public static async Task<List<string>> HiddenUserIdsAsync(this BingeOnDbContext context, string? viewerId)
        {
            if (string.IsNullOrEmpty(viewerId))
                return new List<string>();

            return await context.UserBlocks
                .Where(b => b.BlockerId == viewerId || b.BlockedId == viewerId)
                .Select(b => b.BlockerId == viewerId ? b.BlockedId : b.BlockerId)
                .Distinct()
                .ToListAsync();
        }

        /// <summary>İki kullanıcı arasında herhangi bir yönde engel var mı?</summary>
        public static Task<bool> IsBlockedBetweenAsync(this BingeOnDbContext context, string? first, string? second)
        {
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second) || first == second)
                return Task.FromResult(false);

            return context.UserBlocks.AnyAsync(b =>
                (b.BlockerId == first && b.BlockedId == second) ||
                (b.BlockerId == second && b.BlockedId == first));
        }
    }
}
