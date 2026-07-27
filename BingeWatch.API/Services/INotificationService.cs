using BingeWatch.API.Dtos;
using BingeWatch.API.Models;

namespace BingeWatch.API.Services
{
    public interface INotificationService
    {
        /// <summary>Bildirim yazar. Kendi eylemin sana bildirim üretmez; aynı bildirim tekrarlanmaz.</summary>
        Task CreateAsync(string recipientId, string actorId, NotificationType type, int? reviewId = null,
            int? userListId = null);

        /// <summary>Geri alınan eylemin bildirimini siler (takibi bırakma, beğeniyi kaldırma).</summary>
        Task RemoveAsync(string recipientId, string actorId, NotificationType type, int? reviewId = null,
            int? userListId = null);

        /// <summary>İnceleme silinince ona bağlı tüm bildirimleri siler.</summary>
        Task RemoveForReviewAsync(int reviewId);

        /// <summary>Liste silinince ona bağlı tüm bildirimleri siler.</summary>
        Task RemoveForListAsync(int userListId);

        Task<List<NotificationDto>> GetAsync(string userId, int skip, int take);

        Task<int> GetUnreadCountAsync(string userId);

        /// <summary>Okunmamışların hepsini okundu işaretler; kaç tanesi etkilendiğini döner.</summary>
        Task<int> MarkAllReadAsync(string userId);
    }
}
