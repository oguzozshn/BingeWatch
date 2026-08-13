using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BingeWatch.API.Services
{
    /// <summary>
    /// Profildeki "kütüphane" ve "izleyecekleri" sekmelerinin kaynağı.
    /// <para>
    /// Bu veri ürünün zaten büyük kısmını dışarı veriyordu: profil kartları
    /// izlenen bölüm ve bitirilen dizi sayısını, istatistik sayfası "en çok
    /// izlenenler"i, akış da takip edilenlerin bölüm bölüm izleme olaylarını
    /// gösteriyor. Eksik olan tek şey listenin kendisiydi.
    /// </para>
    /// </summary>
    public class UserLibraryService : IUserLibraryService
    {
        private readonly BingeOnDbContext _context;

        public UserLibraryService(BingeOnDbContext context)
        {
            _context = context;
        }

        public async Task<UserLibraryDto?> GetLibraryAsync(string username, string? viewerId)
        {
            var normalized = username.ToUpperInvariant();
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.NormalizedUserName == normalized || u.UserName == username);

            // Gizli profil yalnızca sahibine görünür; engelli taraflar birbirini
            // hiç göremez. Kural yeni değil — istatistik, liste ve takip
            // servislerindekiyle aynı, burada da tekrarlanmak zorunda.
            if (user == null || (user.IsPrivate && user.Id != viewerId))
                return null;

            if (await _context.IsBlockedBetweenAsync(viewerId, user.Id))
                return null;

            // Sıralama listeye eklenme tarihine göre: en son eklenen üstte.
            // "Son izlenen" daha iyi bir sıra olurdu ama onu hesaplamak bölüm
            // bazlı ilerlemeyi okumak demek — bu uç bilerek ona hiç bakmıyor.
            var shows = await _context.UserShows
                .Where(us => us.UserId == user.Id)
                .OrderByDescending(us => us.AddedAt)
                .Select(us => new LibraryShowDto
                {
                    TmdbId = us.Show!.TmdbId,
                    Name = us.Show.Name,
                    PosterPath = us.Show.PosterPath,
                    Status = us.Status.ToString(),
                    IsFavorite = us.IsFavorite
                })
                .ToListAsync();

            return new UserLibraryDto
            {
                Username = user.UserName ?? string.Empty,
                DisplayName = string.IsNullOrWhiteSpace(user.DisplayName)
                    ? user.UserName ?? string.Empty
                    : user.DisplayName,
                Shows = shows
            };
        }
    }
}
