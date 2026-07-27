using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BingeWatch.API.Services
{
    public class UserListService : IUserListService
    {
        private const int MaxTitleLength = 200;
        private const int MaxDescriptionLength = 2000;
        private const int MaxNoteLength = 1000;

        private readonly BingeOnDbContext _context;
        private readonly IShowCatalogService _catalogService;
        private readonly INotificationService _notificationService;

        public UserListService(BingeOnDbContext context, IShowCatalogService catalogService,
            INotificationService notificationService)
        {
            _context = context;
            _catalogService = catalogService;
            _notificationService = notificationService;
        }

        public async Task<UserListDetailDto?> CreateAsync(string userId, UpsertListRequest request)
        {
            var title = Clip(request.Title, MaxTitleLength);
            if (string.IsNullOrEmpty(title))
                return null;

            var list = new UserList
            {
                UserId = userId,
                Title = title,
                Description = Clip(request.Description, MaxDescriptionLength),
                IsPublic = request.IsPublic
            };

            _context.UserLists.Add(list);
            await _context.SaveChangesAsync();

            return await GetDetailAsync(list.Id, userId);
        }

        public async Task<UserListDetailDto?> UpdateAsync(string userId, int listId, UpsertListRequest request)
        {
            var list = await OwnedListAsync(userId, listId);
            if (list == null)
                return null;

            var title = Clip(request.Title, MaxTitleLength);
            if (string.IsNullOrEmpty(title))
                return null;

            list.Title = title;
            list.Description = Clip(request.Description, MaxDescriptionLength);
            list.IsPublic = request.IsPublic;
            list.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return await GetDetailAsync(list.Id, userId);
        }

        public async Task<bool> DeleteAsync(string userId, int listId)
        {
            var list = await OwnedListAsync(userId, listId);
            if (list == null)
                return false;

            // Öğeler ve beğeniler FK cascade ile gider; bildirimlerin FK'si yok, elle siliniyor.
            _context.UserLists.Remove(list);
            await _context.SaveChangesAsync();

            await _notificationService.RemoveForListAsync(listId);

            return true;
        }

        public async Task<ListLikeStateDto?> LikeAsync(string userId, int listId)
        {
            // Beğenmek görebilmeyi gerektirir: kapalı liste ya da gizli profil beğenilemez.
            var list = await VisibleListAsync(listId, userId);
            if (list == null)
                return null;

            var exists = await _context.UserListLikes
                .AnyAsync(l => l.UserListId == listId && l.UserId == userId);

            if (!exists)
            {
                _context.UserListLikes.Add(new UserListLike { UserListId = listId, UserId = userId });
                await _context.SaveChangesAsync();

                await _notificationService.CreateAsync(list.UserId, userId,
                    NotificationType.ListLiked, userListId: listId);
            }

            return await BuildLikeStateAsync(listId, userId);
        }

        public async Task<ListLikeStateDto?> UnlikeAsync(string userId, int listId)
        {
            var list = await VisibleListAsync(listId, userId);
            if (list == null)
                return null;

            var existing = await _context.UserListLikes
                .FirstOrDefaultAsync(l => l.UserListId == listId && l.UserId == userId);

            if (existing != null)
            {
                _context.UserListLikes.Remove(existing);
                await _context.SaveChangesAsync();

                await _notificationService.RemoveAsync(list.UserId, userId,
                    NotificationType.ListLiked, userListId: listId);
            }

            return await BuildLikeStateAsync(listId, userId);
        }

        public async Task<List<UserListSummaryDto>> GetDiscoverAsync(ListSort sort, int skip, int take,
            string? viewerId)
        {
            take = Math.Clamp(take, 1, 100);
            skip = Math.Max(skip, 0);

            // Keşifte yalnızca herkese açık listeler, gizli olmayan profillerden.
            // Boş liste keşfe girmez — kart posteri de bilgisi de olmayan satır işe yaramaz.
            var query = _context.UserLists
                .Where(l => l.IsPublic && !l.User!.IsPrivate)
                .Where(l => _context.UserListItems.Any(i => i.UserListId == l.Id));

            var ordered = sort switch
            {
                ListSort.MostLiked => query
                    .OrderByDescending(l => _context.UserListLikes.Count(k => k.UserListId == l.Id))
                    .ThenByDescending(l => l.UpdatedAt),
                ListSort.Largest => query
                    .OrderByDescending(l => _context.UserListItems.Count(i => i.UserListId == l.Id))
                    .ThenByDescending(l => l.UpdatedAt),
                _ => query.OrderByDescending(l => l.UpdatedAt)
            };

            var lists = await ordered
                .Skip(skip)
                .Take(take)
                .Include(l => l.User)
                .ToListAsync();

            return await ProjectSummariesAsync(lists, viewerId);
        }

        public async Task<List<UserListSummaryDto>?> GetForUserAsync(string username, string? viewerId)
        {
            var normalized = username.ToUpperInvariant();
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.NormalizedUserName == normalized || u.UserName == username);

            // Gizli profilin listeleri yalnızca sahibine görünür (bkz. FollowService).
            if (user == null || (user.IsPrivate && user.Id != viewerId))
                return null;

            var isOwner = user.Id == viewerId;

            var lists = await _context.UserLists
                .Where(l => l.UserId == user.Id && (l.IsPublic || isOwner))
                .OrderByDescending(l => l.UpdatedAt)
                .Include(l => l.User)
                .ToListAsync();

            return await ProjectSummariesAsync(lists, viewerId);
        }

        public async Task<UserListDetailDto?> GetDetailAsync(int listId, string? viewerId)
        {
            var list = await _context.UserLists
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Id == listId);
            if (list == null)
                return null;

            var isOwner = list.UserId == viewerId;

            // Kapalı liste ve gizli profilin listeleri yalnızca sahibine görünür.
            if (!isOwner && (!list.IsPublic || list.User!.IsPrivate))
                return null;

            // Yıl, tarihten bellekte çıkarılıyor: sorgu içindeki DateTime?.Value
            // erişimi sağlayıcıya göre değerlendirme sırası sorunları çıkarıyor.
            var rows = await _context.UserListItems
                .Where(i => i.UserListId == list.Id)
                .OrderBy(i => i.Position)
                .Select(i => new
                {
                    i.Id,
                    i.Show!.TmdbId,
                    ShowName = i.Show.Name,
                    i.Show.PosterPath,
                    i.Show.FirstAirDate,
                    i.Position,
                    i.Note
                })
                .ToListAsync();

            var items = rows.Select(r => new UserListItemDto
            {
                Id = r.Id,
                TmdbShowId = r.TmdbId,
                ShowName = r.ShowName,
                PosterPath = r.PosterPath,
                FirstAirYear = r.FirstAirDate?.Year,
                Position = r.Position,
                Note = r.Note
            }).ToList();

            var likeState = await BuildLikeStateAsync(list.Id, viewerId);

            var summary = ToSummary(list, list.User!, isOwner, items.Count,
                items.Where(i => i.PosterPath != null).Take(4).Select(i => i.PosterPath!).ToList(),
                likeState.LikeCount, likeState.LikedByViewer);

            return new UserListDetailDto
            {
                Id = summary.Id,
                Title = summary.Title,
                Description = summary.Description,
                IsPublic = summary.IsPublic,
                OwnerUsername = summary.OwnerUsername,
                OwnerDisplayName = summary.OwnerDisplayName,
                OwnerAvatarUrl = summary.OwnerAvatarUrl,
                ItemCount = summary.ItemCount,
                PreviewPosterPaths = summary.PreviewPosterPaths,
                LikeCount = summary.LikeCount,
                LikedByViewer = summary.LikedByViewer,
                IsOwner = summary.IsOwner,
                CreatedAt = summary.CreatedAt,
                UpdatedAt = summary.UpdatedAt,
                Items = items
            };
        }

        public async Task<UserListItemDto?> AddItemAsync(string userId, int listId, AddListItemRequest request)
        {
            var list = await OwnedListAsync(userId, listId);
            if (list == null)
                return null;

            // Kullanıcı diziyi hiç açmadan listeye ekleyebilir; katalogda yoksa çekilir.
            var show = await _catalogService.GetOrSyncShowAsync(request.TmdbShowId);
            if (show == null)
                return null;

            var item = await _context.UserListItems
                .FirstOrDefaultAsync(i => i.UserListId == list.Id && i.ShowId == show.Id);

            if (item == null)
            {
                var lastPosition = await _context.UserListItems
                    .Where(i => i.UserListId == list.Id)
                    .MaxAsync(i => (int?)i.Position) ?? -1;

                item = new UserListItem
                {
                    UserListId = list.Id,
                    ShowId = show.Id,
                    Position = lastPosition + 1,
                    Note = Clip(request.Note, MaxNoteLength)
                };
                _context.UserListItems.Add(item);
            }
            else if (!string.IsNullOrWhiteSpace(request.Note))
            {
                // Aynı diziyi notla tekrar eklemek notu günceller; sıra bozulmaz.
                item.Note = Clip(request.Note, MaxNoteLength);
            }

            list.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ToItemDto(item, show);
        }

        public async Task<bool> RemoveItemAsync(string userId, int listId, int itemId)
        {
            var list = await OwnedListAsync(userId, listId);
            if (list == null)
                return false;

            var item = await _context.UserListItems
                .FirstOrDefaultAsync(i => i.Id == itemId && i.UserListId == list.Id);
            if (item == null)
                return false;

            _context.UserListItems.Remove(item);
            list.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Silinen öğe sırada boşluk bırakır; kalanlar 0..n-1'e sıkıştırılır.
            await CompactPositionsAsync(list.Id);

            return true;
        }

        public async Task<UserListItemDto?> UpdateItemAsync(string userId, int listId, int itemId,
            UpdateListItemRequest request)
        {
            var list = await OwnedListAsync(userId, listId);
            if (list == null)
                return null;

            var item = await _context.UserListItems
                .Include(i => i.Show)
                .FirstOrDefaultAsync(i => i.Id == itemId && i.UserListId == list.Id);
            if (item == null)
                return null;

            item.Note = Clip(request.Note, MaxNoteLength);
            list.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ToItemDto(item, item.Show!);
        }

        public async Task<UserListDetailDto?> ReorderAsync(string userId, int listId, ReorderListRequest request)
        {
            var list = await OwnedListAsync(userId, listId);
            if (list == null)
                return null;

            var items = await _context.UserListItems
                .Where(i => i.UserListId == list.Id)
                .OrderBy(i => i.Position)
                .ToListAsync();

            var byId = items.ToDictionary(i => i.Id);
            var position = 0;

            // Önce istekte geçen sıra uygulanır...
            foreach (var id in request.ItemIds.Distinct())
            {
                if (!byId.TryGetValue(id, out var item))
                    continue;

                item.Position = position++;
                byId.Remove(id);
            }

            // ...istekte hiç geçmeyenler eski sıralarını koruyarak sona eklenir.
            foreach (var item in items.Where(i => byId.ContainsKey(i.Id)))
                item.Position = position++;

            list.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return await GetDetailAsync(list.Id, userId);
        }

        public async Task<List<ListMembershipDto>> GetMembershipAsync(string userId, int tmdbShowId)
        {
            var showId = await _context.Shows
                .Where(s => s.TmdbId == tmdbShowId)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();

            return await _context.UserLists
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.UpdatedAt)
                .Select(l => new ListMembershipDto
                {
                    ListId = l.Id,
                    Title = l.Title,
                    IsPublic = l.IsPublic,
                    ContainsShow = showId != null &&
                        _context.UserListItems.Any(i => i.UserListId == l.Id && i.ShowId == showId)
                })
                .ToListAsync();
        }

        /// <summary>Liste yoksa ya da isteği yapan sahibi değilse <c>null</c> — ikisi de "bulunamadı".</summary>
        private Task<UserList?> OwnedListAsync(string userId, int listId) =>
            _context.UserLists.FirstOrDefaultAsync(l => l.Id == listId && l.UserId == userId);

        /// <summary>
        /// İsteği yapanın görebildiği liste; <see cref="GetDetailAsync"/> ile aynı
        /// gizlilik kuralı (kapalı liste ve gizli profil yalnızca sahibine görünür).
        /// </summary>
        private async Task<UserList?> VisibleListAsync(int listId, string? viewerId)
        {
            var list = await _context.UserLists
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Id == listId);
            if (list == null)
                return null;

            if (list.UserId != viewerId && (!list.IsPublic || list.User!.IsPrivate))
                return null;

            return list;
        }

        private async Task<ListLikeStateDto> BuildLikeStateAsync(int listId, string? viewerId) => new()
        {
            ListId = listId,
            LikeCount = await _context.UserListLikes.CountAsync(l => l.UserListId == listId),
            LikedByViewer = viewerId != null &&
                await _context.UserListLikes.AnyAsync(l => l.UserListId == listId && l.UserId == viewerId)
        };

        /// <summary>Sıra numaralarındaki boşlukları kapatır; sıra hep 0..n-1 olur.</summary>
        private async Task CompactPositionsAsync(int listId)
        {
            var items = await _context.UserListItems
                .Where(i => i.UserListId == listId)
                .OrderBy(i => i.Position)
                .ToListAsync();

            var changed = false;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Position == i)
                    continue;

                items[i].Position = i;
                changed = true;
            }

            if (changed)
                await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Liste kartları için sayaç, önizleme posterleri ve beğenileri toplar.
        /// Keşifte listeler farklı sahiplere ait olabildiği için sahip bilgisi
        /// listenin kendisinden okunur (<c>Include(l =&gt; l.User)</c> gerekir).
        /// </summary>
        private async Task<List<UserListSummaryDto>> ProjectSummariesAsync(List<UserList> lists, string? viewerId)
        {
            if (lists.Count == 0)
                return new List<UserListSummaryDto>();

            var listIds = lists.Select(l => l.Id).ToList();

            var counts = await _context.UserListItems
                .Where(i => listIds.Contains(i.UserListId))
                .GroupBy(i => i.UserListId)
                .Select(g => new { UserListId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserListId, x => x.Count);

            // Kart başına 4 poster; ilk sıradaki öğeler tek sorguda çekilip bellekte gruplanır.
            var posterRows = await _context.UserListItems
                .Where(i => listIds.Contains(i.UserListId) && i.Show!.PosterPath != null)
                .OrderBy(i => i.Position)
                .Select(i => new { i.UserListId, i.Position, PosterPath = i.Show!.PosterPath! })
                .ToListAsync();

            var posters = posterRows
                .GroupBy(x => x.UserListId)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Position).Take(4).Select(x => x.PosterPath).ToList());

            var likeCounts = await _context.UserListLikes
                .Where(l => listIds.Contains(l.UserListId))
                .GroupBy(l => l.UserListId)
                .Select(g => new { UserListId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserListId, x => x.Count);

            var likedByViewer = viewerId == null
                ? new HashSet<int>()
                : (await _context.UserListLikes
                    .Where(l => l.UserId == viewerId && listIds.Contains(l.UserListId))
                    .Select(l => l.UserListId)
                    .ToListAsync()).ToHashSet();

            return lists.Select(l => ToSummary(l, l.User!, l.UserId == viewerId,
                counts.TryGetValue(l.Id, out var count) ? count : 0,
                posters.TryGetValue(l.Id, out var preview) ? preview : new List<string>(),
                likeCounts.TryGetValue(l.Id, out var likes) ? likes : 0,
                likedByViewer.Contains(l.Id))).ToList();
        }

        private static UserListSummaryDto ToSummary(UserList list, AppUser owner, bool isOwner, int itemCount,
            List<string> previewPosters, int likeCount, bool likedByViewer) => new()
            {
                Id = list.Id,
                Title = list.Title,
                Description = list.Description,
                IsPublic = list.IsPublic,
                OwnerUsername = owner.UserName ?? string.Empty,
                OwnerDisplayName = string.IsNullOrWhiteSpace(owner.DisplayName)
                    ? owner.UserName ?? string.Empty
                    : owner.DisplayName,
                OwnerAvatarUrl = owner.AvatarUrl,
                ItemCount = itemCount,
                PreviewPosterPaths = previewPosters,
                LikeCount = likeCount,
                LikedByViewer = likedByViewer,
                IsOwner = isOwner,
                CreatedAt = list.CreatedAt,
                UpdatedAt = list.UpdatedAt
            };

        private static UserListItemDto ToItemDto(UserListItem item, Show show) => new()
        {
            Id = item.Id,
            TmdbShowId = show.TmdbId,
            ShowName = show.Name,
            PosterPath = show.PosterPath,
            FirstAirYear = show.FirstAirDate?.Year,
            Position = item.Position,
            Note = item.Note
        };

        /// <summary>Baştaki/sondaki boşlukları atar, sınırı aşan metni keser; boşsa <c>null</c>.</summary>
        private static string? Clip(string? value, int maxLength)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return null;

            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }
    }
}
