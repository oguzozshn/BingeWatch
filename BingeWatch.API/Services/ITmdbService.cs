using BingeWatch.API.Dtos;

namespace BingeWatch.API.Services
{
    public interface ITmdbService
    {
        Task<List<SeriesDto>> GetPopularSeriesAsync(int page);
        Task<List<SeriesDto>> SearchSeriesAsync(string query, int page);

        /// <summary>Dizi sayfasındaki "Benzer" sekmesi — TMDb'nin önerdiği diziler.</summary>
        Task<List<SeriesDto>> GetSimilarSeriesAsync(int tmdbId, int page = 1);
    }
}
