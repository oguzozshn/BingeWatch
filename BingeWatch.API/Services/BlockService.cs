using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BingeWatch.API.Services
{
    /// <summary>
    /// Engelleme. Engel <b>iki yönlü</b> etki eder (bkz. <see cref="UserBlock"/>);
    /// engelleme anında aradaki takip ilişkileri, o takiplerin akış olayları ve
    /// takip bildirimleri her iki yönde de temizlenir — engellediğin biri takipçi
    /// listende durmaya devam etmemeli.
    /// </summary>
    public class BlockService : IBlockService
    {
        private readonly BingeOnDbContext _context;

        public BlockService(BingeOnDbContext context)
        {
            _context = context;
        }

        public async Task<BlockResult> BlockAsync(string userId, string targetUsername)
        {
            var target = await ResolveUserAsync(targetUsername);
            if (target == null)
                return BlockResult.TargetNotFound;

            if (target.Id == userId)
                return BlockResult.Self;

            var exists = await _context.UserBlocks
                .AnyAsync(b => b.BlockerId == userId && b.BlockedId == target.Id);
            if (exists)
                return BlockResult.Ok;

            _context.UserBlocks.Add(new UserBlock { BlockerId = userId, BlockedId = target.Id });
            await _context.SaveChangesAsync();

            await SeverFollowsAsync(userId, target.Id);

            return BlockResult.Ok;
        }

        public async Task<BlockResult> UnblockAsync(string userId, string targetUsername)
        {
            var target = await ResolveUserAsync(targetUsername);
            if (target == null)
                return BlockResult.TargetNotFound;

            if (target.Id == userId)
                return BlockResult.Self;

            var block = await _context.UserBlocks
                .FirstOrDefaultAsync(b => b.BlockerId == userId && b.BlockedId == target.Id);
            if (block == null)
                return BlockResult.Ok;

            // Engeli kaldırmak takipleri geri getirmez; taraflar isterse yeniden takip eder.
            _context.UserBlocks.Remove(block);
            await _context.SaveChangesAsync();

            return BlockResult.Ok;
        }

        public async Task<List<BlockedUserDto>> GetBlockedAsync(string userId)
        {
            return await _context.UserBlocks
                .Where(b => b.BlockerId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new BlockedUserDto
                {
                    Username = b.Blocked!.UserName ?? string.Empty,
                    DisplayName = string.IsNullOrEmpty(b.Blocked.DisplayName)
                        ? b.Blocked.UserName ?? string.Empty
                        : b.Blocked.DisplayName,
                    AvatarUrl = b.Blocked.AvatarUrl,
                    BlockedAt = b.CreatedAt
                })
                .ToListAsync();
        }

        public Task<bool> IsBlockedBetweenAsync(string? first, string? second) =>
            _context.IsBlockedBetweenAsync(first, second);

        /// <summary>Aradaki takipleri, takip olaylarını ve takip bildirimlerini iki yönde de siler.</summary>
        private async Task SeverFollowsAsync(string blockerId, string blockedId)
        {
            var follows = await _context.Follows
                .Where(f => (f.FollowerId == blockerId && f.FolloweeId == blockedId)
                         || (f.FollowerId == blockedId && f.FolloweeId == blockerId))
                .ToListAsync();

            var events = await _context.ActivityEvents
                .Where(a => a.Type == ActivityType.Followed
                         && ((a.UserId == blockerId && a.TargetUserId == blockedId)
                          || (a.UserId == blockedId && a.TargetUserId == blockerId)))
                .ToListAsync();

            var notifications = await _context.Notifications
                .Where(n => n.Type == NotificationType.Followed
                         && ((n.UserId == blockerId && n.ActorId == blockedId)
                          || (n.UserId == blockedId && n.ActorId == blockerId)))
                .ToListAsync();

            if (follows.Count == 0 && events.Count == 0 && notifications.Count == 0)
                return;

            _context.Follows.RemoveRange(follows);
            _context.ActivityEvents.RemoveRange(events);
            _context.Notifications.RemoveRange(notifications);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Kullanıcı adını kullanıcıya çevirir. Gizlilik kontrolü <b>yapılmaz</b>:
        /// gizli profilli birini de, seni engellemiş birini de engelleyebilmelisin.
        /// </summary>
        private Task<AppUser?> ResolveUserAsync(string username)
        {
            var normalized = username.ToUpperInvariant();
            return _context.Users
                .FirstOrDefaultAsync(u => u.NormalizedUserName == normalized || u.UserName == username);
        }
    }
}
