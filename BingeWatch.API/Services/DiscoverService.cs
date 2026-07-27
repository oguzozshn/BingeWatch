using System.Globalization;
using BingeWatch.API.Clients;
using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BingeWatch.API.Services
{
    /// <summary>
    /// Filtreli keşif ve gelişmiş arama.
    ///
    /// Keşif TMDb <c>/discover/tv</c> üzerinden yapılır: yerel katalog yalnızca
    /// kullanıcıların dokunduğu dizileri içerir, onun üstünde filtrelemek "keşif"
    /// değil rastgele bir alt küme olurdu. Kullanıcının kendi kütüphanesinde
    /// (durum filtresi) ise soru yerelde cevaplanır — orada katalog zaten tam.
    /// </summary>
    public class DiscoverService : IDiscoverService
    {
        private const int PageSize = 20;

        private static readonly TimeSpan DiscoverCacheTtl = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan GenreCacheTtl = TimeSpan.FromDays(1);
        private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromMinutes(5);

        /// <summary>
        /// TMDb'de kanal listesi uç noktası yok. Katalogda henüz hiç dizisi olmayan
        /// platformlar da filtrelenebilsin diye bilinen büyükler sabit tutuluyor.
        /// </summary>
        private static readonly (int Id, string Name)[] WellKnownNetworks =
        {
            (213, "Netflix"),
            (49, "HBO"),
            (2552, "Apple TV+"),
            (1024, "Prime Video"),
            (2739, "Disney+"),
            (67, "Showtime"),
            (174, "AMC"),
            (4330, "Paramount+"),
            (453, "Hulu"),
            (56, "Cartoon Network"),
            (16, "BBC One"),
            (6, "NBC"),
            (2, "ABC"),
            (19, "FOX"),
            (71, "The CW")
        };

        private readonly TmdbClient _client;
        private readonly BingeOnDbContext _context;
        private readonly IMemoryCache _cache;

        public DiscoverService(TmdbClient client, BingeOnDbContext context, IMemoryCache cache)
        {
            _client = client;
            _context = context;
            _cache = cache;
        }

        public async Task<DiscoverResultDto> DiscoverAsync(DiscoverQuery query, string? viewerId)
        {
            var page = Math.Max(query.Page, 1);

            if (query.Status != null)
                return await DiscoverInLibraryAsync(query, viewerId, page);

            return await DiscoverInTmdbAsync(query, page);
        }

        public async Task<List<GenreDto>> GetGenresAsync()
        {
            const string cacheKey = "tmdb:genres:tv";
            if (_cache.TryGetValue(cacheKey, out List<GenreDto>? cached) && cached != null)
                return cached;

            var response = await _client.GetTvGenresAsync();
            var genres = response?.Genres
                .Select(g => new GenreDto { Id = g.Id, Name = g.Name })
                .OrderBy(g => g.Name)
                .ToList();

            // TMDb ulaşılamazsa katalogda görülen türlerle idare et; filtre paneli boş kalmasın.
            if (genres == null || genres.Count == 0)
            {
                genres = await _context.Genres
                    .OrderBy(g => g.Name)
                    .Select(g => new GenreDto { Id = g.Id, Name = g.Name })
                    .ToListAsync();
            }

            _cache.Set(cacheKey, genres, GenreCacheTtl);
            return genres;
        }

        public async Task<List<NetworkDto>> GetNetworksAsync()
        {
            var known = await _context.Networks
                .Select(n => new NetworkDto { Id = n.Id, Name = n.Name })
                .ToListAsync();

            var missing = WellKnownNetworks
                .Where(w => known.All(k => k.Id != w.Id))
                .Select(w => new NetworkDto { Id = w.Id, Name = w.Name });

            return known.Concat(missing).OrderBy(n => n.Name).ToList();
        }

        public async Task<SearchResultDto> SearchAsync(string query, bool includePeople)
        {
            var trimmed = query.Trim();
            if (trimmed.Length == 0)
                return new SearchResultDto();

            var cacheKey = $"search:{trimmed.ToLowerInvariant()}:{includePeople}";
            if (_cache.TryGetValue(cacheKey, out SearchResultDto? cached) && cached != null)
                return cached;

            var result = new SearchResultDto();

            var shows = await _client.SearchSeriesAsync(trimmed);
            result.Shows = (shows?.Results ?? new()).Select(ToDiscoverDto).ToList();

            if (includePeople)
            {
                var people = await _client.SearchPeopleAsync(trimmed);
                result.People = (people?.Results ?? new())
                    // Yalnızca dizi tarafında anlamlı isimler; popülerlik TMDb'nin sırası.
                    .Take(5)
                    .Select(p => new PersonDto
                    {
                        TmdbId = p.Id,
                        Name = p.Name,
                        ProfilePath = p.ProfilePath,
                        KnownForDepartment = p.KnownForDepartment
                    })
                    .ToList();

                // "Bilinen dizileri" ipucu için kredi çekmek kişi başına bir istek demek;
                // yalnızca ilk üç kişi için yapılır ki arama yavaşlamasın.
                foreach (var person in result.People.Take(3))
                {
                    var credits = await _client.GetPersonTvCreditsAsync(person.TmdbId);
                    if (credits == null)
                        continue;

                    person.KnownForShows = credits.Cast
                        .OrderByDescending(c => c.EpisodeCount)
                        .Select(c => c.Name)
                        .Distinct()
                        .Take(3)
                        .ToList();
                }
            }

            _cache.Set(cacheKey, result, SearchCacheTtl);
            return result;
        }

        public async Task<PersonCreditsDto?> GetPersonCreditsAsync(int personId)
        {
            var cacheKey = $"person:credits:{personId}";
            if (_cache.TryGetValue(cacheKey, out PersonCreditsDto? cached) && cached != null)
                return cached;

            var credits = await _client.GetPersonTvCreditsAsync(personId);
            if (credits == null)
                return null;

            // Aynı dizi hem oyunculuk hem ekip kredisiyle gelebilir; bölüm sayısı
            // yüksek olan (asıl işi) tutulur.
            var merged = credits.Cast.Select(c => (Credit: c, Role: c.Character))
                .Concat(credits.Crew.Select(c => (Credit: c, Role: c.Job)))
                .GroupBy(x => x.Credit.Id)
                .Select(g => g.OrderByDescending(x => x.Credit.EpisodeCount).First())
                .OrderByDescending(x => x.Credit.EpisodeCount)
                .ThenByDescending(x => x.Credit.FirstAirDate ?? DateTime.MinValue)
                .Select(x => new PersonCreditDto
                {
                    TmdbShowId = x.Credit.Id,
                    Name = x.Credit.Name,
                    PosterPath = x.Credit.PosterPath,
                    FirstAirYear = x.Credit.FirstAirDate?.Year,
                    VoteAverage = x.Credit.VoteAverage,
                    Role = string.IsNullOrWhiteSpace(x.Role) ? null : x.Role,
                    EpisodeCount = x.Credit.EpisodeCount
                })
                .ToList();

            // Kredi yanıtı kişinin adını taşımıyor; ad ve fotoğraf için ayrı istek.
            var person = await _client.GetPersonAsync(personId);

            var result = new PersonCreditsDto
            {
                TmdbId = personId,
                Name = person?.Name ?? string.Empty,
                ProfilePath = person?.ProfilePath,
                Credits = merged
            };

            _cache.Set(cacheKey, result, DiscoverCacheTtl);
            return result;
        }

        private async Task<DiscoverResultDto> DiscoverInTmdbAsync(DiscoverQuery query, int page)
        {
            var parameters = new List<string>
            {
                $"page={page}",
                $"sort_by={SortParameter(query.Sort)}",
                // Tek oylu diziler puana göre sıralamada listeyi çöpe çevirir.
                "vote_count.gte=50"
            };

            if (query.GenreIds.Count > 0)
                parameters.Add("with_genres=" + string.Join(",", query.GenreIds.Distinct()));

            if (query.NetworkIds.Count > 0)
                parameters.Add("with_networks=" + string.Join("|", query.NetworkIds.Distinct()));

            if (query.YearFrom is int from)
                parameters.Add($"first_air_date.gte={from}-01-01");

            if (query.YearTo is int to)
                parameters.Add($"first_air_date.lte={to}-12-31");

            if (query.MinRating is double min)
                parameters.Add("vote_average.gte=" + min.ToString(CultureInfo.InvariantCulture));

            var queryString = string.Join("&", parameters);
            var cacheKey = "tmdb:discover:" + queryString;

            if (_cache.TryGetValue(cacheKey, out DiscoverResultDto? cached) && cached != null)
                return cached;

            var response = await _client.DiscoverSeriesAsync(queryString);
            var results = (response?.Results ?? new()).Select(ToDiscoverDto).ToList();

            var result = new DiscoverResultDto
            {
                Page = page,
                Results = results,
                // TMDb sayfa başına 20 döner; tam sayfa geldiyse devamı var sayılır.
                HasMore = results.Count >= PageSize
            };

            _cache.Set(cacheKey, result, DiscoverCacheTtl);
            return result;
        }

        /// <summary>
        /// Kütüphane modu: kullanıcının o durumdaki dizileri, aynı filtrelerle
        /// (tür/yıl/puan) yerelden süzülür. Anonim istekte sonuç boş.
        /// </summary>
        private async Task<DiscoverResultDto> DiscoverInLibraryAsync(DiscoverQuery query, string? viewerId, int page)
        {
            if (viewerId == null)
                return new DiscoverResultDto { Page = page };

            var rows = _context.UserShows
                .Where(us => us.UserId == viewerId && us.Status == query.Status);

            if (query.GenreIds.Count > 0)
            {
                // Tür filtresi "ve": seçilen türlerin hepsini taşıyan diziler.
                foreach (var genreId in query.GenreIds.Distinct())
                    rows = rows.Where(us => us.Show!.Genres.Any(g => g.Id == genreId));
            }

            if (query.NetworkIds.Count > 0)
            {
                var networkIds = query.NetworkIds.Distinct().ToList();
                rows = rows.Where(us => us.Show!.Networks.Any(n => networkIds.Contains(n.Id)));
            }

            if (query.YearFrom is int from)
                rows = rows.Where(us => us.Show!.FirstAirDate != null && us.Show.FirstAirDate!.Value.Year >= from);

            if (query.YearTo is int to)
                rows = rows.Where(us => us.Show!.FirstAirDate != null && us.Show.FirstAirDate!.Value.Year <= to);

            if (query.MinRating is double min)
                rows = rows.Where(us => us.Show!.VoteAverage >= min);

            var ordered = query.Sort switch
            {
                DiscoverSort.Rating => rows.OrderByDescending(us => us.Show!.VoteAverage),
                DiscoverSort.Newest => rows.OrderByDescending(us => us.Show!.FirstAirDate),
                _ => rows.OrderByDescending(us => us.Show!.VoteCount)
            };

            var projected = await ordered
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(us => new
                {
                    us.Show!.TmdbId,
                    us.Show.Name,
                    us.Show.PosterPath,
                    us.Show.FirstAirDate,
                    us.Show.VoteAverage,
                    us.Status
                })
                .ToListAsync();

            return new DiscoverResultDto
            {
                Page = page,
                Results = projected.Select(x => new DiscoverShowDto
                {
                    TmdbId = x.TmdbId,
                    Name = x.Name,
                    PosterPath = x.PosterPath,
                    FirstAirYear = x.FirstAirDate?.Year,
                    VoteAverage = x.VoteAverage,
                    Status = x.Status
                }).ToList(),
                HasMore = projected.Count >= PageSize
            };
        }

        private static string SortParameter(DiscoverSort sort) => sort switch
        {
            DiscoverSort.Rating => "vote_average.desc",
            DiscoverSort.Newest => "first_air_date.desc",
            _ => "popularity.desc"
        };

        private static DiscoverShowDto ToDiscoverDto(SeriesItem item) => new()
        {
            TmdbId = item.Id,
            Name = item.Name,
            PosterPath = item.PosterPath,
            FirstAirYear = item.FirstAirDate?.Year,
            VoteAverage = item.VoteAverage
        };
    }
}
