using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BingeWatch.API.Services
{
    /// <summary>
    /// Bölüm tartışmaları. Tek kural her şeyi belirliyor: <b>ipliği yalnızca
    /// bölümü izlemiş olan açar.</b>
    ///
    /// Spoiler'ı kullanıcının işaretlemesine bırakmak (incelemelerdeki
    /// <c>HasSpoilers</c> bayrağı) burada işe yaramaz — bir bölümün altındaki
    /// yorum zaten tanımı gereği o bölümü konuşur. Bunun yerine izleme takibi
    /// verisi kapı olarak kullanılıyor: koruma kullanıcının dürüstlüğüne değil
    /// kendi ilerlemesine dayanıyor.
    ///
    /// Kapı hem okumada hem yazmada aynı; anonim ziyaretçi hiçbir ipliği göremez.
    /// </summary>
    public class EpisodeCommentService : IEpisodeCommentService
    {
        private readonly BingeOnDbContext _context;

        public EpisodeCommentService(BingeOnDbContext context)
        {
            _context = context;
        }

        public async Task<EpisodeCommentThreadDto?> GetThreadAsync(int episodeId, string? viewerId)
        {
            var episode = await _context.Episodes
                .Where(e => e.Id == episodeId)
                .Select(e => new { e.Id, e.AirDate })
                .FirstOrDefaultAsync();
            if (episode == null)
                return null;

            var thread = new EpisodeCommentThreadDto
            {
                EpisodeId = episodeId,
                Unaired = episode.AirDate.HasValue && episode.AirDate.Value.Date > DateTime.UtcNow.Date
            };

            if (!await HasWatchedAsync(viewerId, episodeId))
            {
                // Kilitli iplik yorumları da sayıyı da taşımaz; "burada 40 yorum
                // var" bilgisi tek başına bölüm hakkında bir şey söylüyor.
                thread.Locked = true;
                return thread;
            }

            var hidden = await _context.HiddenUserIdsAsync(viewerId);

            var comments = await _context.EpisodeComments
                .Where(c => c.EpisodeId == episodeId && !hidden.Contains(c.UserId))
                .OrderBy(c => c.CreatedAt)
                .Include(c => c.User)
                .ToListAsync();

            thread.Comments = comments.Select(c => Project(c, viewerId)).ToList();
            return thread;
        }

        public async Task<EpisodeCommentDto?> AddAsync(string userId, int episodeId,
            AddEpisodeCommentRequest request)
        {
            var body = request.Body?.Trim() ?? string.Empty;
            if (body.Length == 0)
                return null;

            if (!await _context.Episodes.AnyAsync(e => e.Id == episodeId))
                return null;

            // Yazmak da okumakla aynı kapıdan geçiyor: izlemeden yorum yazılamaz.
            if (!await HasWatchedAsync(userId, episodeId))
                return null;

            var comment = new EpisodeComment { EpisodeId = episodeId, UserId = userId, Body = body };
            _context.EpisodeComments.Add(comment);
            await _context.SaveChangesAsync();

            // Bilinçli olarak ne ActivityEvent ne Notification yazılıyor: iplik
            // yayılmayan bir varış noktası ve sahibi yok (bkz. EpisodeComment).
            await _context.Entry(comment).Reference(c => c.User).LoadAsync();
            return Project(comment, userId);
        }

        public async Task<bool> DeleteAsync(string userId, int commentId)
        {
            var comment = await _context.EpisodeComments
                .FirstOrDefaultAsync(c => c.Id == commentId);
            if (comment == null)
                return false;

            // İnceleme yorumundaki "inceleme sahibi de silebilir" kuralının
            // burada karşılığı yok; bölüm kimsenin mülkü değil.
            if (comment.UserId != userId)
                return false;

            _context.EpisodeComments.Remove(comment);
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Kapının kendisi. <c>RewatchNo == 0</c> filtresi kod tabanındaki diğer
        /// izleme okumalarıyla aynı (yeniden izleme henüz bir özellik değil).
        /// </summary>
        private async Task<bool> HasWatchedAsync(string? userId, int episodeId)
        {
            if (string.IsNullOrEmpty(userId))
                return false;

            return await _context.WatchedEpisodes
                .AnyAsync(w => w.UserId == userId && w.EpisodeId == episodeId && w.RewatchNo == 0);
        }

        private static EpisodeCommentDto Project(EpisodeComment comment, string? viewerId) =>
            new()
            {
                Id = comment.Id,
                EpisodeId = comment.EpisodeId,
                Username = comment.User?.UserName ?? string.Empty,
                DisplayName = string.IsNullOrWhiteSpace(comment.User?.DisplayName)
                    ? comment.User?.UserName ?? string.Empty
                    : comment.User!.DisplayName,
                AvatarUrl = comment.User?.AvatarUrl,
                Body = comment.Body,
                CreatedAt = comment.CreatedAt,
                CanDelete = viewerId != null && comment.UserId == viewerId
            };
    }
}
