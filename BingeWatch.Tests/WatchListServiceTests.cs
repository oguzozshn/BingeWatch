using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BingeWatch.Tests
{
    public class WatchListServiceTests
    {
        private static BingeOnDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<BingeOnDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new BingeOnDbContext(options);
        }

        private static WatchListService CreateService(BingeOnDbContext context) =>
            new(context, NullLogger<WatchListService>.Instance);

        [Fact]
        public async Task AddToWatchListAsync_NormalizesFullPosterUrlToRelativePath()
        {
            using var context = CreateContext();
            var service = CreateService(context);

            var series = new SeriesDto
            {
                Id = 1,
                Name = "Test Show",
                PosterPath = "https://image.tmdb.org/t/p/w500/abc123.jpg"
            };

            await service.AddToWatchListAsync("user1", series);

            var watchList = await service.GetUserWatchListAsync("user1");
            Assert.Equal("/abc123.jpg", Assert.Single(watchList).PosterPath);
        }

        [Fact]
        public async Task AddToWatchListAsync_LeavesAlreadyRelativePosterPathUnchanged()
        {
            using var context = CreateContext();
            var service = CreateService(context);

            var series = new SeriesDto { Id = 1, Name = "Test Show", PosterPath = "/abc123.jpg" };

            await service.AddToWatchListAsync("user1", series);

            var watchList = await service.GetUserWatchListAsync("user1");
            Assert.Equal("/abc123.jpg", Assert.Single(watchList).PosterPath);
        }

        [Fact]
        public async Task AddToWatchListAsync_ReturnsFalse_WhenSeriesAlreadyInWatchList()
        {
            using var context = CreateContext();
            var service = CreateService(context);
            var series = new SeriesDto { Id = 1, Name = "Test Show" };

            var firstAdd = await service.AddToWatchListAsync("user1", series);
            var secondAdd = await service.AddToWatchListAsync("user1", series);

            Assert.True(firstAdd);
            Assert.False(secondAdd);
            Assert.Single(await service.GetUserWatchListAsync("user1"));
        }

        [Fact]
        public async Task ToggleAsync_AddsSeries_WhenNotInWatchList()
        {
            using var context = CreateContext();
            var service = CreateService(context);
            var series = new SeriesDto { Id = 1, Name = "Test Show" };

            var isInWatchList = await service.ToggleAsync("user1", series);

            Assert.True(isInWatchList);
            Assert.True(await service.IsInWatchListAsync("user1", 1));
        }

        [Fact]
        public async Task ToggleAsync_RemovesSeries_WhenAlreadyInWatchList()
        {
            using var context = CreateContext();
            var service = CreateService(context);
            var series = new SeriesDto { Id = 1, Name = "Test Show" };
            await service.AddToWatchListAsync("user1", series);

            var isInWatchList = await service.ToggleAsync("user1", series);

            Assert.False(isInWatchList);
            Assert.False(await service.IsInWatchListAsync("user1", 1));
        }

        [Fact]
        public async Task RemoveFromWatchListAsync_ReturnsFalse_WhenSeriesNotInWatchList()
        {
            using var context = CreateContext();
            var service = CreateService(context);

            var result = await service.RemoveFromWatchListAsync("user1", 999);

            Assert.False(result);
        }

        [Fact]
        public async Task GetUserWatchListAsync_OnlyReturnsItemsForRequestedUser()
        {
            using var context = CreateContext();
            var service = CreateService(context);
            await service.AddToWatchListAsync("user1", new SeriesDto { Id = 1, Name = "Show A" });
            await service.AddToWatchListAsync("user2", new SeriesDto { Id = 2, Name = "Show B" });

            var user1List = await service.GetUserWatchListAsync("user1");

            Assert.Single(user1List);
            Assert.Equal("Show A", user1List[0].Name);
        }

        [Fact]
        public async Task AddToWatchListAsync_SharesASingleCatalogRow_WhenTwoUsersAddTheSameShow()
        {
            using var context = CreateContext();
            var service = CreateService(context);
            var series = new SeriesDto { Id = 42, Name = "Shared Show" };

            await service.AddToWatchListAsync("user1", series);
            await service.AddToWatchListAsync("user2", series);

            // Dizi bilgisi kullanıcı başına kopyalanmamalı — katalogda tek satır olmalı.
            Assert.Single(context.Shows.Where(s => s.TmdbId == 42));
            Assert.Equal(2, context.UserShows.Count());
        }

        [Fact]
        public async Task RemoveFromWatchListAsync_LeavesOtherUsersEntryIntact()
        {
            using var context = CreateContext();
            var service = CreateService(context);
            var series = new SeriesDto { Id = 42, Name = "Shared Show" };
            await service.AddToWatchListAsync("user1", series);
            await service.AddToWatchListAsync("user2", series);

            var removed = await service.RemoveFromWatchListAsync("user1", 42);

            Assert.True(removed);
            Assert.False(await service.IsInWatchListAsync("user1", 42));
            Assert.True(await service.IsInWatchListAsync("user2", 42));
            // Katalog satırı, hâlâ listesinde tutan kullanıcı için korunmalı.
            Assert.Single(context.Shows.Where(s => s.TmdbId == 42));
        }

        [Fact]
        public async Task AddToWatchListAsync_LeavesShowUnsynced_SoCatalogCanEnrichItLater()
        {
            using var context = CreateContext();
            var service = CreateService(context);

            await service.AddToWatchListAsync("user1", new SeriesDto { Id = 7, Name = "Stub Show" });

            var show = context.Shows.Single(s => s.TmdbId == 7);
            Assert.Equal(default, show.LastSyncedAt);
        }
    }
}
