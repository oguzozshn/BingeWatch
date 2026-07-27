using System.Security.Claims;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BingeWatch.API.Controllers
{
    [ApiController]
    [Route("api/discover")]
    public class DiscoverController : ControllerBase
    {
        private readonly IDiscoverService _discoverService;

        public DiscoverController(IDiscoverService discoverService)
        {
            _discoverService = discoverService;
        }

        /// <summary>Anonim istekte <c>null</c> — kütüphane modu boş döner.</summary>
        private string? ViewerId => User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        /// <summary>
        /// Filtreli keşif. <c>status</c> verilirse arama isteği yapanın
        /// kütüphanesinde yapılır ve kimlik doğrulaması gerekir.
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Discover(
            [FromQuery(Name = "genre")] List<int>? genreIds,
            [FromQuery(Name = "network")] List<int>? networkIds,
            [FromQuery] int? yearFrom,
            [FromQuery] int? yearTo,
            [FromQuery] double? minRating,
            [FromQuery] DiscoverSort sort = DiscoverSort.Popularity,
            [FromQuery] WatchStatus? status = null,
            [FromQuery] int page = 1)
        {
            var query = new DiscoverQuery
            {
                GenreIds = genreIds ?? new(),
                NetworkIds = networkIds ?? new(),
                YearFrom = yearFrom,
                YearTo = yearTo,
                MinRating = minRating,
                Sort = sort,
                Status = status,
                Page = page
            };

            return Ok(await _discoverService.DiscoverAsync(query, ViewerId));
        }

        [HttpGet("genres")]
        [AllowAnonymous]
        public async Task<IActionResult> GetGenres() => Ok(await _discoverService.GetGenresAsync());

        [HttpGet("networks")]
        [AllowAnonymous]
        public async Task<IActionResult> GetNetworks() => Ok(await _discoverService.GetNetworksAsync());

        /// <summary>Gelişmiş arama — diziler ve (istenirse) kişiler.</summary>
        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] bool includePeople = true)
        {
            return Ok(await _discoverService.SearchAsync(q ?? string.Empty, includePeople));
        }

        [HttpGet("people/{personId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPerson(int personId)
        {
            var credits = await _discoverService.GetPersonCreditsAsync(personId);
            return credits == null ? NotFound() : Ok(credits);
        }
    }
}
