using BingeWatch.API.Dtos;

namespace BingeWatch.API.Services
{
    public interface IUserListService
    {
        /// <summary>Başlık boşsa <c>null</c>.</summary>
        Task<UserListDetailDto?> CreateAsync(string userId, UpsertListRequest request);

        /// <summary>Liste yoksa ya da isteği yapan sahibi değilse <c>null</c>.</summary>
        Task<UserListDetailDto?> UpdateAsync(string userId, int listId, UpsertListRequest request);

        Task<bool> DeleteAsync(string userId, int listId);

        /// <summary>
        /// Bir kullanıcının listeleri. Gizli profil ya da bulunamayan kullanıcı için
        /// <c>null</c>; kapalı listeler yalnızca sahibine dahil edilir.
        /// </summary>
        Task<List<UserListSummaryDto>?> GetForUserAsync(string username, string? viewerId);

        /// <summary>Liste görünür değilse (yok ya da kapalı+başkası) <c>null</c>.</summary>
        Task<UserListDetailDto?> GetDetailAsync(int listId, string? viewerId);

        /// <summary>
        /// Keşif akışı: gizli olmayan profillerin herkese açık ve boş olmayan
        /// listeleri, istenen sırayla.
        /// </summary>
        /// <summary>Liste keşif akışı; imleç tabanlı sayfalama.</summary>
        Task<PagedResult<UserListSummaryDto>> GetDiscoverAsync(ListSort sort, string? cursor, int take,
            string? viewerId);

        /// <summary>Beğeni idempotent; liste görünür değilse <c>null</c>.</summary>
        Task<ListLikeStateDto?> LikeAsync(string userId, int listId);

        Task<ListLikeStateDto?> UnlikeAsync(string userId, int listId);

        /// <summary>
        /// Diziyi listenin sonuna ekler. Dizi zaten listedeyse mevcut öğe döner
        /// (idempotent). Liste sahibinin değilse ya da TMDb'de dizi yoksa <c>null</c>.
        /// </summary>
        Task<UserListItemDto?> AddItemAsync(string userId, int listId, AddListItemRequest request);

        Task<bool> RemoveItemAsync(string userId, int listId, int itemId);

        Task<UserListItemDto?> UpdateItemAsync(string userId, int listId, int itemId, UpdateListItemRequest request);

        /// <summary>
        /// Öğeleri verilen id sırasına göre yeniden dizer. İstekte geçmeyen öğeler
        /// mevcut sıralarını koruyarak sona alınır.
        /// </summary>
        Task<UserListDetailDto?> ReorderAsync(string userId, int listId, ReorderListRequest request);

        /// <summary>"Listeye ekle" menüsü: kullanıcının listeleri + dizinin üyelik durumu.</summary>
        Task<List<ListMembershipDto>> GetMembershipAsync(string userId, int tmdbShowId);
    }
}
