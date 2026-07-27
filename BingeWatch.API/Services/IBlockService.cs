using BingeWatch.API.Dtos;

namespace BingeWatch.API.Services
{
    public interface IBlockService
    {
        Task<BlockResult> BlockAsync(string userId, string targetUsername);

        Task<BlockResult> UnblockAsync(string userId, string targetUsername);

        Task<List<BlockedUserDto>> GetBlockedAsync(string userId);

        /// <summary>İki kullanıcı arasında hangi yönde olursa olsun engel var mı?</summary>
        Task<bool> IsBlockedBetweenAsync(string? first, string? second);
    }
}
