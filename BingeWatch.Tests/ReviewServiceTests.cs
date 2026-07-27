using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BingeWatch.Tests
{
    public class ReviewServiceTests
    {
        private static BingeOnDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<BingeOnDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new BingeOnDbContext(options);
        }

        /// <summary>Katalogu yalnızca yerel DB'den okuyan sahte; testlerde TMDb'ye çıkılmaz.</summary>
        private sealed class LocalOnlyCatalogService : IShowCatalogService
        {
            private readonly BingeOnDbContext _context;
            public LocalOnlyCatalogService(BingeOnDbContext context) => _context = context;

            public Task<Show?> GetOrSyncShowAsync(int tmdbId, bool forceSync = false) =>
                _context.Shows.Include(s => s.Seasons).FirstOrDefaultAsync(s => s.TmdbId == tmdbId);

            public Task<int> SyncStaleOngoingShowsAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(0);
        }

        private static ReviewService CreateService(BingeOnDbContext context) =>
            new(context, new LocalOnlyCatalogService(context), new ActivityService(context), new NotificationService(context));

        private static async Task<Show> SeedAsync(BingeOnDbContext context, string userId = "user1")
        {
            context.Users.Add(new AppUser
            {
                Id = userId,
                UserName = userId,
                DisplayName = userId == "user1" ? "Kullanıcı Bir" : userId
            });

            var show = new Show { TmdbId = 1, Name = "Test Show", LastSyncedAt = DateTime.UtcNow };
            context.Shows.Add(show);
            await context.SaveChangesAsync();

            context.Seasons.Add(new Season { ShowId = show.Id, SeasonNumber = 1, EpisodeCount = 2 });
            await context.SaveChangesAsync();

            return show;
        }

        [Fact]
        public async Task UpsertAsync_RejectsEmptyBody()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var result = await service.UpsertAsync("user1", 1, new UpsertReviewRequest { Body = "   " });

            Assert.Null(result);
            Assert.Empty(context.Reviews);
        }

        [Fact]
        public async Task UpsertAsync_SecondCallOnSameTargetEditsExisting()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            await service.UpsertAsync("user1", 1, new UpsertReviewRequest { Body = "ilk hâli" });
            var updated = await service.UpsertAsync("user1", 1, new UpsertReviewRequest { Body = "ikinci hâli", HasSpoilers = true });

            var review = await context.Reviews.SingleAsync();
            Assert.Equal("ikinci hâli", review.Body);
            Assert.True(review.HasSpoilers);
            Assert.Equal(review.Id, updated!.Id);
        }

        [Fact]
        public async Task UpsertAsync_ShowAndSeasonReviewsAreSeparateRows()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            await service.UpsertAsync("user1", 1, new UpsertReviewRequest { Body = "dizi geneli" });
            await service.UpsertAsync("user1", 1, new UpsertReviewRequest { SeasonNumber = 1, Body = "1. sezon" });

            Assert.Equal(2, await context.Reviews.CountAsync());
        }

        [Fact]
        public async Task UpsertAsync_RejectsUnknownSeasonNumber()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var result = await service.UpsertAsync("user1", 1, new UpsertReviewRequest { SeasonNumber = 7, Body = "yok böyle sezon" });

            Assert.Null(result);
            Assert.Empty(context.Reviews);
        }

        [Fact]
        public async Task UpsertAsync_AttachesAuthorsRatingForSameTarget()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var ratingService = new RatingService(context, new ActivityService(context));
            await ratingService.SetRatingAsync("user1", 1, new SetRatingRequest { TargetType = RatingTargetType.Show, Value = 4.5m });
            var service = CreateService(context);

            var review = await service.UpsertAsync("user1", 1, new UpsertReviewRequest { Body = "harika" });

            Assert.Equal(4.5m, review!.Rating);
            Assert.Equal("Kullanıcı Bir", review.DisplayName);
            Assert.Equal(1, review.TmdbShowId);
        }

        [Fact]
        public async Task GetForShowAsync_CanFilterToASingleSeason()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);
            await service.UpsertAsync("user1", 1, new UpsertReviewRequest { Body = "dizi geneli" });
            await service.UpsertAsync("user1", 1, new UpsertReviewRequest { SeasonNumber = 1, Body = "1. sezon" });

            var all = await service.GetForShowAsync(1);
            var seasonOnly = await service.GetForShowAsync(1, seasonNumber: 1);

            Assert.Equal(2, all.Count);
            Assert.Equal("1. sezon", Assert.Single(seasonOnly).Body);
        }

        [Fact]
        public async Task DeleteAsync_OnlyOwnerCanDelete()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);
            var review = await service.UpsertAsync("user1", 1, new UpsertReviewRequest { Body = "benim incelemem" });

            Assert.False(await service.DeleteAsync("user2", review!.Id));
            Assert.True(await service.DeleteAsync("user1", review.Id));
            Assert.Empty(context.Reviews);
        }

        [Fact]
        public async Task GetFeedAsync_ReturnsNewestFirst_AndRespectsPaging()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);
            await service.UpsertAsync("user1", 1, new UpsertReviewRequest { Body = "eski" });
            // CreatedAt varsayılanı UtcNow olduğu için sırayı deterministik yapmak üzere geri alıyoruz.
            var first = await context.Reviews.SingleAsync();
            first.CreatedAt = DateTime.UtcNow.AddHours(-1);
            await context.SaveChangesAsync();
            await service.UpsertAsync("user1", 1, new UpsertReviewRequest { SeasonNumber = 1, Body = "yeni" });

            var feed = await service.GetFeedAsync(skip: 0, take: 10, ReviewSort.Newest);
            var second = await service.GetFeedAsync(skip: 1, take: 10, ReviewSort.Newest);

            Assert.Equal("yeni", feed[0].Body);
            Assert.Equal("eski", Assert.Single(second).Body);
        }

        [Fact]
        public async Task GetFeedAsync_HighestRatedPutsUnratedLast()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var ratingService = new RatingService(context, new ActivityService(context));
            var service = CreateService(context);
            // Dizi geneli inceleme puansız, sezon incelemesi 5 yıldız.
            await service.UpsertAsync("user1", 1, new UpsertReviewRequest { Body = "puansız" });
            await service.UpsertAsync("user1", 1, new UpsertReviewRequest { SeasonNumber = 1, Body = "puanlı" });
            await ratingService.SetRatingAsync("user1", 1, new SetRatingRequest
            {
                TargetType = RatingTargetType.Season,
                SeasonNumber = 1,
                Value = 5m
            });

            var feed = await service.GetFeedAsync(0, 10, ReviewSort.HighestRated);

            Assert.Equal("puanlı", feed[0].Body);
            Assert.Null(feed[1].Rating);
        }
    }
}
