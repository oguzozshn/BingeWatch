using BingeWatch.API.Dtos;

namespace BingeWatch.API.Services
{
    public interface ITmdbService
    {
        Task<List<SeriesDto>> GetPopularSeriesAsync(int page);
        Task<List<SeriesDto>> SearchSeriesAsync(string query, int page);
    }
}
