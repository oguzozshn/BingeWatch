using Microsoft.AspNetCore.Mvc;
using BingeWatch.API.Services;
using BingeWatch.API.Clients;
using BingeWatch.API.Configurations;
using Microsoft.Extensions.Options;

namespace BingeWatch.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SeriesController : ControllerBase
    {
        private readonly ITmdbService _tmdbService;

        public SeriesController(ITmdbService tmdbService)
        {
            _tmdbService = tmdbService;
        }

        [HttpGet]
        public async Task<IActionResult> GetPopularSeries([FromQuery] int page = 1)
        {
            var series = await _tmdbService.GetPopularSeriesAsync(page);
            return Ok(series);
        }
    }

}