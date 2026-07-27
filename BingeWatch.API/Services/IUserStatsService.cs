using BingeWatch.API.Dtos;

namespace BingeWatch.API.Services
{
    public interface IUserStatsService
    {
        /// <summary>
        /// Profil istatistikleri. Gizli profiller yalnızca sahibine görünür;
        /// başkasına <c>null</c> döner (bkz. FollowService'teki aynı kural).
        /// </summary>
        Task<UserStatsDto?> GetStatsAsync(string username, string? viewerId);

        /// <summary>Diziyi favorilere ekler/çıkarır. Dizi kullanıcının listesinde değilse <c>false</c>.</summary>
        Task<bool> SetFavoriteAsync(string userId, int showTmdbId, bool isFavorite);

        /// <summary>Dizi kullanıcının favorilerinde mi? Listesinde değilse de <c>false</c>.</summary>
        Task<bool> IsFavoriteAsync(string userId, int showTmdbId);
    }
}
