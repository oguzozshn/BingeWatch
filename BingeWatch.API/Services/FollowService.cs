using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BingeWatch.API.Services
{
    public class FollowService : IFollowService
    {
        private readonly BingeOnDbContext _context;
        private readonly IActivityService _activityService;
        private readonly INotificationService _notificationService;

        public FollowService(BingeOnDbContext context, IActivityService activityService,
            INotificationService notificationService)
        {
            _context = context;
            _activityService = activityService;
            _notificationService = notificationService;
        }

        public async Task<FollowResult> FollowAsync(string followerId, string targetUsername)
        {
            var target = await ResolveVisibleUserAsync(targetUsername, followerId);
            if (target == null)
                return FollowResult.TargetNotFound;

            if (target.Id == followerId)
                return FollowResult.Self;

            var exists = await _context.Follows
                .AnyAsync(f => f.FollowerId == followerId && f.FolloweeId == target.Id);
            if (exists)
                return FollowResult.Ok;

            _context.Follows.Add(new Follow { FollowerId = followerId, FolloweeId = target.Id });
            await _context.SaveChangesAsync();

            await _activityService.RecordFollowedAsync(followerId, target.Id);
            await _notificationService.CreateAsync(target.Id, followerId, NotificationType.Followed);

            return FollowResult.Ok;
        }

        public async Task<FollowResult> UnfollowAsync(string followerId, string targetUsername)
        {
            var target = await ResolveVisibleUserAsync(targetUsername, followerId);
            if (target == null)
                return FollowResult.TargetNotFound;

            if (target.Id == followerId)
                return FollowResult.Self;

            var follow = await _context.Follows
                .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FolloweeId == target.Id);
            if (follow == null)
                return FollowResult.Ok;

            _context.Follows.Remove(follow);
            await _context.SaveChangesAsync();

            await _activityService.RemoveFollowedAsync(followerId, target.Id);
            await _notificationService.RemoveAsync(target.Id, followerId, NotificationType.Followed);

            return FollowResult.Ok;
        }

        public async Task<List<UserSummaryDto>?> GetFollowersAsync(string username, string? viewerId)
        {
            var user = await ResolveVisibleUserAsync(username, viewerId);
            if (user == null)
                return null;

            var followers = await _context.Follows
                .Where(f => f.FolloweeId == user.Id)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => f.Follower!)
                .ToListAsync();

            return await ProjectAsync(followers, viewerId);
        }

        public async Task<List<UserSummaryDto>?> GetFollowingAsync(string username, string? viewerId)
        {
            var user = await ResolveVisibleUserAsync(username, viewerId);
            if (user == null)
                return null;

            var following = await _context.Follows
                .Where(f => f.FollowerId == user.Id)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => f.Followee!)
                .ToListAsync();

            return await ProjectAsync(following, viewerId);
        }

        public Task<int> GetFollowerCountAsync(string userId) =>
            _context.Follows.CountAsync(f => f.FolloweeId == userId);

        public Task<int> GetFollowingCountAsync(string userId) =>
            _context.Follows.CountAsync(f => f.FollowerId == userId);

        public Task<bool> IsFollowingAsync(string followerId, string followeeId) =>
            _context.Follows.AnyAsync(f => f.FollowerId == followerId && f.FolloweeId == followeeId);

        /// <summary>
        /// Kullanıcı arama. Uygulamada birini takip etmenin tek yolu adresini
        /// elle yazmaktı; bu, takip özelliğini pratikte kullanılamaz kılıyordu.
        ///
        /// <b>Gizli profiller hiç dönmüyor</b> — sahibine bile. Profil zaten
        /// başkasına açılmıyor; arama sonucunda görünmesi, var olduğunu
        /// duyurmaktan başka işe yaramaz ve gizliliğin amacını zedelerdi.
        /// Engelli taraflar <see cref="ProjectAsync"/> içinde eleniyor.
        /// </summary>
        public async Task<List<UserSummaryDto>> SearchAsync(string query, string? viewerId, int limit = 20)
        {
            query = query?.Trim() ?? string.Empty;
            if (query.Length < 2)
                return new List<UserSummaryDto>(); // tek harf tüm kullanıcıları döker

            var normalized = query.ToUpperInvariant();

            var users = await _context.Users
                .Where(u => !u.IsPrivate)
                .Where(u => u.NormalizedUserName!.Contains(normalized)
                            || (u.DisplayName != null && u.DisplayName.ToUpper().Contains(normalized)))
                // Baştan eşleşenler önce: "ali" araması "ali"yi "kemalist"in
                // üstünde göstermeli.
                .OrderBy(u => u.NormalizedUserName!.StartsWith(normalized) ? 0 : 1)
                .ThenBy(u => u.UserName)
                .Take(limit)
                .ToListAsync();

            return await ProjectAsync(users, viewerId);
        }

        /// <summary>
        /// Kullanıcı adını kullanıcıya çevirir. Gizli profiller yalnızca sahibine görünür;
        /// aralarında engel olan kullanıcılar da birbirini göremez. İkisinde de
        /// <c>null</c> döner ve çağıran bunu "yok" gibi ele alır.
        /// </summary>
        private async Task<AppUser?> ResolveVisibleUserAsync(string username, string? viewerId)
        {
            var normalized = username.ToUpperInvariant();
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.NormalizedUserName == normalized || u.UserName == username);

            if (user == null || (user.IsPrivate && user.Id != viewerId))
                return null;

            if (await _context.IsBlockedBetweenAsync(viewerId, user.Id))
                return null;

            return user;
        }

        /// <summary>
        /// Kullanıcı listesini, isteği yapanın takip durumuyla birlikte DTO'ya çevirir.
        /// Engelli taraflar listeden düşer — başkasının takipçi listesi engeli delmemeli.
        /// </summary>
        private async Task<List<UserSummaryDto>> ProjectAsync(List<AppUser> users, string? viewerId)
        {
            var hidden = await _context.HiddenUserIdsAsync(viewerId);
            if (hidden.Count > 0)
                users = users.Where(u => !hidden.Contains(u.Id)).ToList();

            var followedByViewer = new HashSet<string>();
            if (viewerId != null && users.Count > 0)
            {
                var ids = users.Select(u => u.Id).ToList();
                followedByViewer = (await _context.Follows
                    .Where(f => f.FollowerId == viewerId && ids.Contains(f.FolloweeId))
                    .Select(f => f.FolloweeId)
                    .ToListAsync()).ToHashSet();
            }

            return users.Select(u => new UserSummaryDto
            {
                Username = u.UserName ?? string.Empty,
                DisplayName = string.IsNullOrWhiteSpace(u.DisplayName) ? u.UserName ?? string.Empty : u.DisplayName,
                AvatarUrl = u.AvatarUrl,
                Bio = u.Bio,
                IsFollowedByViewer = followedByViewer.Contains(u.Id),
                IsViewer = u.Id == viewerId
            }).ToList();
        }
    }
}
