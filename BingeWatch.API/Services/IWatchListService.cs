using BingeWatch.API.Dtos;

namespace BingeWatch.API.Services
{
    public interface IWatchListService
    {
        Task<List<SeriesDto>> GetUserWatchListAsync(string userId);
        Task<bool> AddToWatchListAsync(string userId, SeriesDto series);
        Task<bool> RemoveFromWatchListAsync(string userId, int tmdbShowId);
        Task<bool> IsInWatchListAsync(string userId, int tmdbShowId);
        Task<bool> ToggleAsync(string userId, SeriesDto series);
    }
}
