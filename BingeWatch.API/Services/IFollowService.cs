using BingeWatch.API.Dtos;

namespace BingeWatch.API.Services
{
    public enum FollowResult
    {
        Ok,
        /// <summary>Hedef kullanıcı yok ya da profili gizli.</summary>
        TargetNotFound,
        /// <summary>Kullanıcı kendini takip edemez.</summary>
        Self
    }

    public interface IFollowService
    {
        /// <summary>Takip eder; ilişki zaten varsa sessizce <see cref="FollowResult.Ok"/> döner.</summary>
        Task<FollowResult> FollowAsync(string followerId, string targetUsername);

        /// <summary>Takibi bırakır; ilişki yoksa da <see cref="FollowResult.Ok"/> döner.</summary>
        Task<FollowResult> UnfollowAsync(string followerId, string targetUsername);

        /// <summary><paramref name="username"/> kullanıcısını takip edenler, en yeniden eskiye.</summary>
        Task<List<UserSummaryDto>?> GetFollowersAsync(string username, string? viewerId);

        /// <summary><paramref name="username"/> kullanıcısının takip ettikleri, en yeniden eskiye.</summary>
        Task<List<UserSummaryDto>?> GetFollowingAsync(string username, string? viewerId);

        /// <summary>
        /// Kullanıcı adı ya da görünen adda geçen kullanıcıları arar. Gizli
        /// profiller ve engelli taraflar sonuçlara girmez.
        /// </summary>
        Task<List<UserSummaryDto>> SearchAsync(string query, string? viewerId, int limit = 20);

        Task<int> GetFollowerCountAsync(string userId);

        Task<int> GetFollowingCountAsync(string userId);

        Task<bool> IsFollowingAsync(string followerId, string followeeId);
    }
}
