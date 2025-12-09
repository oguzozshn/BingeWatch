using BingeWatch.API.Dtos;

namespace BingeWatch.API.Services
{
    public interface IWatchListService
    {
        Task<List<SeriesDto>> GetUserWatchListAsync(string userId);
        Task<bool> AddToWatchListAsync(string userId, SeriesDto series);
        Task<bool> RemoveFromWatchListAsync(string userId, int seriesId);
        Task<bool> IsInWatchListAsync(string userId, int seriesId);
        Task<bool> ToggleAsync(string userId, int seriesId);
    }
} 