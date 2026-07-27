using BingeWatch.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BingeWatch.API.Controllers
{
    /// <summary>
    /// Sitemap'i besleyen veri. Web katmanı XML'i bundan üretiyor — sitemap
    /// yayınlanan adrese (Web) ait ama içerik katalogda, o yüzden liste burada.
    /// </summary>
    [ApiController]
    [Route("api/sitemap")]
    [AllowAnonymous]
    public class SitemapController : ControllerBase
    {
        /// <summary>
        /// Sitemap protokolü dosya başına 50.000 URL sınırı koyuyor; katalog
        /// büyürse bölmek gerekecek. Şimdilik tek dosya yetiyor.
        /// </summary>
        private const int MaxEntries = 50_000;

        private readonly BingeOnDbContext _context;

        public SitemapController(BingeOnDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Dizine girebilecek dizi sayfaları. Yalnızca yerel katalogda olanlar:
        /// TMDb'nin tamamını sitemap'e yazmak, çoğu hiç ziyaret edilmemiş
        /// on binlerce sayfayı taratmak olurdu.
        /// </summary>
        [HttpGet("shows")]
        public async Task<IActionResult> GetShows()
        {
            var shows = await _context.Shows
                .OrderByDescending(s => s.LastSyncedAt)
                .Take(MaxEntries)
                .Select(s => new SitemapEntryDto
                {
                    TmdbId = s.TmdbId,
                    LastModified = s.LastSyncedAt
                })
                .ToListAsync();

            return Ok(shows);
        }

        /// <summary>Herkese açık listeler; kapalı olanlar ve gizli profillerinkiler hariç.</summary>
        [HttpGet("lists")]
        public async Task<IActionResult> GetLists()
        {
            var lists = await _context.UserLists
                .Where(l => l.IsPublic && !l.User!.IsPrivate)
                .Where(l => _context.UserListItems.Any(i => i.UserListId == l.Id))
                .OrderByDescending(l => l.UpdatedAt)
                .Take(MaxEntries)
                .Select(l => new SitemapEntryDto
                {
                    TmdbId = l.Id,
                    LastModified = l.UpdatedAt
                })
                .ToListAsync();

            return Ok(lists);
        }
    }

    public class SitemapEntryDto
    {
        /// <summary>Dizide TMDb id'si, listede yerel liste id'si.</summary>
        public int TmdbId { get; set; }

        public DateTime LastModified { get; set; }
    }
}
