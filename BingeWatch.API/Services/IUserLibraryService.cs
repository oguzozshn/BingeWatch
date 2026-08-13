using BingeWatch.API.Dtos;

namespace BingeWatch.API.Services
{
    public interface IUserLibraryService
    {
        /// <summary>
        /// Kullanıcının kütüphanesi — listesindeki tüm diziler, durumlarıyla.
        /// Görünürlük kuralı istatistiklerle aynı: gizli profil yalnızca
        /// sahibine, engelli taraflara hiç. Görünmüyorsa <c>null</c>.
        /// </summary>
        Task<UserLibraryDto?> GetLibraryAsync(string username, string? viewerId);
    }
}
