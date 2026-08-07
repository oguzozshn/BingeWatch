using Microsoft.Extensions.Caching.Memory;
using BingeWatch.API.Dtos;
using BingeWatch.API.Clients;

namespace BingeWatch.API.Services
{
    public class TmdbService : ITmdbService
    {
        // Popüler liste sık değişmez; arama sonucu daha kısa tutulur ki yeni
        // eklenen bir dizi kullanıcıya "bulunamadı" gibi görünmesin.
        private static readonly TimeSpan PopularCacheTtl = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromMinutes(5);

        private readonly TmdbClient _client;
        private readonly IMemoryCache _cache;

        public TmdbService(TmdbClient client, IMemoryCache cache)
        {
            _client = client;
            _cache = cache;
        }

        public async Task<List<SeriesDto>> GetPopularSeriesAsync(int page)
        {
            var cacheKey = $"tmdb:popular:{page}";

            if (_cache.TryGetValue(cacheKey, out List<SeriesDto>? cached) && cached != null)
                return cached;

            var tmdbResult = await _client.GetPopularSeriesAsync(page);
            var result = (tmdbResult?.Results ?? new()).Select(ToDto).ToList();

            _cache.Set(cacheKey, result, PopularCacheTtl);
            return result;
        }

        public async Task<List<SeriesDto>> SearchSeriesAsync(string query, int page)
        {
            var cacheKey = $"tmdb:search:{query.Trim().ToLowerInvariant()}:{page}";

            if (_cache.TryGetValue(cacheKey, out List<SeriesDto>? cached) && cached != null)
                return cached;

            var tmdbResult = await _client.SearchSeriesAsync(query, page);
            var result = (tmdbResult?.Results ?? new()).Select(ToDto).ToList();

            _cache.Set(cacheKey, result, SearchCacheTtl);
            return result;
        }

        public async Task<List<SeriesDto>> GetSimilarSeriesAsync(int tmdbId, int page = 1)
        {
            var cacheKey = $"tmdb:similar:{tmdbId}:{page}";

            if (_cache.TryGetValue(cacheKey, out List<SeriesDto>? cached) && cached != null)
                return cached;

            var tmdbResult = await _client.GetSimilarSeriesAsync(tmdbId, page);
            var result = (tmdbResult?.Results ?? new()).Select(ToDto).ToList();

            // Benzerlik listesi popüler liste kadar durağan; aynı TTL yeterli.
            _cache.Set(cacheKey, result, PopularCacheTtl);
            return result;
        }

        private static SeriesDto ToDto(Models.SeriesItem s) => new()
        {
            Id = s.Id,
            Name = s.Name,
            Overview = s.Overview,
            PosterPath = s.PosterPath,
            FirstAirDate = s.FirstAirDate,
            VoteAverage = s.VoteAverage,
            VoteCount = s.VoteCount
        };
    }
}
