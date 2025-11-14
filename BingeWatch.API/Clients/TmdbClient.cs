using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Options;
using BingeWatch.API.Configurations;
using BingeWatch.API.Models;

namespace BingeWatch.API.Clients
{
    public class TmdbClient
    {
        private readonly TmdbSettings _settings;
        private readonly HttpClient _httpClient;

        public TmdbClient(HttpClient httpClient, IOptions<TmdbSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;

            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }

        public async Task<TmdbSeriesResult> GetPopularSeriesAsync(int page = 1)
        {
            var response = await _httpClient.GetAsync($"/3/tv/popular?language=en-US&page={page}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<TmdbSeriesResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        public async Task<TmdbSeriesResult> SearchSeriesAsync(string query, int page = 1)
        {
            var encodedQuery = Uri.EscapeDataString(query);
            var response = await _httpClient.GetAsync($"/3/search/tv?query={encodedQuery}&language=en-US&page={page}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<TmdbSeriesResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
    }
}
