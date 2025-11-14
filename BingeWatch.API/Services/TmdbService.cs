using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Options;
using BingeWatch.API.Configurations;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using BingeWatch.API.Clients;

namespace BingeWatch.API.Services
{
    public class TmdbService : ITmdbService
    {
        private readonly TmdbClient _client;

        public TmdbService(TmdbClient client)
        {
            _client = client;
        }

        public async Task<List<SeriesDto>> GetPopularSeriesAsync(int page)
        {
            var tmdbResult = await _client.GetPopularSeriesAsync(page);

            return tmdbResult.Results.Select(s => new SeriesDto
            {
                Id = s.Id,
                Name = s.Name,
                Overview = s.Overview,
                PosterPath = s.PosterPath,
                FirstAirDate = s.FirstAirDate
            }).ToList();
        }

        public async Task<List<SeriesDto>> SearchSeriesAsync(string query, int page)
        {
            var tmdbResult = await _client.SearchSeriesAsync(query, page);

            return tmdbResult.Results.Select(s => new SeriesDto
            {
                Id = s.Id,
                Name = s.Name,
                Overview = s.Overview,
                PosterPath = s.PosterPath,
                FirstAirDate = s.FirstAirDate
            }).ToList();
        }
    }

}