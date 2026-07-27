using BingeWatch.API.Clients;
using BingeWatch.API.Configurations;
using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace BingeWatch.Tests
{
    /// <summary>
    /// Yalnızca kütüphane modu test ediliyor: keşif modu TMDb'ye çıkar, kütüphane
    /// modu tamamen yereldir ve asıl filtre mantığı orada.
    /// </summary>
    public class DiscoverServiceTests
    {
        private static BingeOnDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<BingeOnDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new BingeOnDbContext(options);
        }

        /// <summary>
        /// Kütüphane modunda TMDb'ye gidilmediği için istemci hiç kullanılmaz;
        /// yine de bağımlılık olarak gerekiyor.
        /// </summary>
        private static DiscoverService CreateService(BingeOnDbContext context)
        {
            var settings = Options.Create(new TmdbSettings
            {
                ApiKey = "test",
                BaseUrl = "https://api.themoviedb.org"
            });

            var client = new TmdbClient(new HttpClient(), settings);
            return new DiscoverService(client, context, new MemoryCache(new MemoryCacheOptions()));
        }

        private static async Task SeedAsync(BingeOnDbContext context)
        {
            context.Users.Add(new AppUser { Id = "ali", UserName = "ali", DisplayName = "Ali" });

            var drama = new Genre { Id = 18, Name = "Drama" };
            var comedy = new Genre { Id = 35, Name = "Comedy" };
            var hbo = new Network { Id = 49, Name = "HBO" };
            context.Genres.AddRange(drama, comedy);
            context.Networks.Add(hbo);

            var dramaShow = new Show
            {
                TmdbId = 1,
                Name = "Ağır Drama",
                FirstAirDate = new DateTime(2008, 1, 20),
                VoteAverage = 8.9,
                VoteCount = 5000,
                LastSyncedAt = DateTime.UtcNow,
                Genres = { drama },
                Networks = { hbo }
            };

            var comedyShow = new Show
            {
                TmdbId = 2,
                Name = "Hafif Komedi",
                FirstAirDate = new DateTime(2019, 5, 1),
                VoteAverage = 6.4,
                VoteCount = 900,
                LastSyncedAt = DateTime.UtcNow,
                Genres = { comedy }
            };

            // Hem drama hem komedi: "ve" filtresinin doğru çalıştığını göstermek için.
            var bothShow = new Show
            {
                TmdbId = 3,
                Name = "Kara Komedi",
                FirstAirDate = new DateTime(2015, 3, 3),
                VoteAverage = 7.8,
                VoteCount = 2000,
                LastSyncedAt = DateTime.UtcNow,
                Genres = { drama, comedy }
            };

            context.Shows.AddRange(dramaShow, comedyShow, bothShow);
            await context.SaveChangesAsync();

            context.UserShows.AddRange(
                new UserShow { UserId = "ali", ShowId = dramaShow.Id, Status = WatchStatus.Watching },
                new UserShow { UserId = "ali", ShowId = comedyShow.Id, Status = WatchStatus.Watching },
                new UserShow { UserId = "ali", ShowId = bothShow.Id, Status = WatchStatus.Completed });

            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task DiscoverAsync_LibraryModeFiltersByStatus()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var result = await service.DiscoverAsync(
                new DiscoverQuery { Status = WatchStatus.Watching }, "ali");

            Assert.Equal(2, result.Results.Count);
            Assert.All(result.Results, r => Assert.Equal(WatchStatus.Watching, r.Status));
        }

        [Fact]
        public async Task DiscoverAsync_LibraryModeIsEmptyForAnonymous()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var result = await service.DiscoverAsync(
                new DiscoverQuery { Status = WatchStatus.Watching }, null);

            Assert.Empty(result.Results);
        }

        [Fact]
        public async Task DiscoverAsync_MultipleGenresCombineWithAnd()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var single = await service.DiscoverAsync(new DiscoverQuery
            {
                Status = WatchStatus.Completed,
                GenreIds = new List<int> { 18 }
            }, "ali");

            var both = await service.DiscoverAsync(new DiscoverQuery
            {
                Status = WatchStatus.Completed,
                GenreIds = new List<int> { 18, 35 }
            }, "ali");

            var impossible = await service.DiscoverAsync(new DiscoverQuery
            {
                Status = WatchStatus.Watching,
                GenreIds = new List<int> { 18, 35 }
            }, "ali");

            Assert.Single(single.Results);
            Assert.Single(both.Results);
            Assert.Equal(3, both.Results[0].TmdbId);
            // İzlenenlerde hem drama hem komedi olan dizi yok.
            Assert.Empty(impossible.Results);
        }

        [Fact]
        public async Task DiscoverAsync_NetworkFilterMatchesShowsOfThatPlatform()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var result = await service.DiscoverAsync(new DiscoverQuery
            {
                Status = WatchStatus.Watching,
                NetworkIds = new List<int> { 49 }
            }, "ali");

            Assert.Single(result.Results);
            Assert.Equal(1, result.Results[0].TmdbId);
        }

        [Fact]
        public async Task DiscoverAsync_YearAndRatingFiltersNarrowResults()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var byYear = await service.DiscoverAsync(new DiscoverQuery
            {
                Status = WatchStatus.Watching,
                YearFrom = 2010
            }, "ali");

            var byRating = await service.DiscoverAsync(new DiscoverQuery
            {
                Status = WatchStatus.Watching,
                MinRating = 8
            }, "ali");

            Assert.Single(byYear.Results);
            Assert.Equal(2, byYear.Results[0].TmdbId);
            Assert.Single(byRating.Results);
            Assert.Equal(1, byRating.Results[0].TmdbId);
        }

        [Fact]
        public async Task DiscoverAsync_RatingSortOrdersByVoteAverage()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var result = await service.DiscoverAsync(new DiscoverQuery
            {
                Status = WatchStatus.Watching,
                Sort = DiscoverSort.Rating
            }, "ali");

            Assert.Equal(new[] { 1, 2 }, result.Results.Select(r => r.TmdbId));
        }

        [Fact]
        public async Task GetNetworksAsync_MergesCatalogWithWellKnownPlatforms()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var networks = await service.GetNetworksAsync();

            // Katalogdaki HBO tek satır olmalı, sabit listedeki diğerleri de gelmeli.
            Assert.Single(networks, n => n.Id == 49);
            Assert.Contains(networks, n => n.Name == "Netflix");
            Assert.Equal(networks.OrderBy(n => n.Name).Select(n => n.Name), networks.Select(n => n.Name));
        }
    }
}
