using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BingeWatch.Tests
{
    public class ActivityServiceTests
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

        private static async Task<Show> SeedAsync(BingeOnDbContext context, params string[] userIds)
        {
            foreach (var id in userIds)
            {
                context.Users.Add(new AppUser
                {
                    Id = id,
                    UserName = id,
                    NormalizedUserName = id.ToUpperInvariant(),
                    DisplayName = id
                });
            }

            var show = new Show { TmdbId = 1, Name = "Test Show", LastSyncedAt = DateTime.UtcNow };
            context.Shows.Add(show);
            await context.SaveChangesAsync();

            var season = new Season { ShowId = show.Id, SeasonNumber = 1, EpisodeCount = 2 };
            context.Seasons.Add(season);
            await context.SaveChangesAsync();

            context.Episodes.Add(new Episode { SeasonId = season.Id, EpisodeNumber = 1, Name = "Bölüm 1" });
            context.Episodes.Add(new Episode { SeasonId = season.Id, EpisodeNumber = 2, Name = "Bölüm 2" });
            await context.SaveChangesAsync();

            return show;
        }

        [Fact]
        public async Task RecordRatedAsync_SecondRatingUpdatesSameEvent()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "ali");
            var service = new ActivityService(context);

            await service.RecordRatedAsync("ali", show.Id, RatingTargetType.Show, null, null, 4.0m);
            await service.RecordRatedAsync("ali", show.Id, RatingTargetType.Show, null, null, 5.0m);

            var only = Assert.Single(context.ActivityEvents);
            Assert.Equal(ActivityType.Rated, only.Type);
            Assert.Equal(5.0m, only.RatingValue);
        }

        [Fact]
        public async Task RecordRatedAsync_SeparateEventPerLevel()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "ali");
            var service = new ActivityService(context);

            await service.RecordRatedAsync("ali", show.Id, RatingTargetType.Show, null, null, 4.0m);
            await service.RecordRatedAsync("ali", show.Id, RatingTargetType.Season, 1, null, 3.0m);

            Assert.Equal(2, await context.ActivityEvents.CountAsync());
        }

        [Fact]
        public async Task RemoveRatedAsync_DeletesEvent()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "ali");
            var service = new ActivityService(context);

            await service.RecordRatedAsync("ali", show.Id, RatingTargetType.Season, 1, null, 4.0m);
            await service.RemoveRatedAsync("ali", show.Id, RatingTargetType.Season, 1, null);

            Assert.Empty(context.ActivityEvents);
        }

        [Fact]
        public async Task RecordFollowedAsync_IsIdempotentAndRemovable()
        {
            using var context = CreateContext();
            await SeedAsync(context, "ali", "veli");
            var service = new ActivityService(context);

            await service.RecordFollowedAsync("ali", "veli");
            await service.RecordFollowedAsync("ali", "veli");

            Assert.Equal(1, await context.ActivityEvents.CountAsync());

            await service.RemoveFollowedAsync("ali", "veli");
            Assert.Empty(context.ActivityEvents);
        }

        [Fact]
        public async Task GetFeedAsync_ReturnsFolloweesAndSelfNewestFirst()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "ali", "veli", "yabanci");
            var service = new ActivityService(context);

            context.Follows.Add(new Follow { FollowerId = "ali", FolloweeId = "veli" });
            await context.SaveChangesAsync();

            await service.RecordRatedAsync("veli", show.Id, RatingTargetType.Show, null, null, 4.0m);
            await service.RecordRatedAsync("ali", show.Id, RatingTargetType.Show, null, null, 3.0m);
            await service.RecordRatedAsync("yabanci", show.Id, RatingTargetType.Show, null, null, 5.0m);

            var feed = await service.GetFeedAsync("ali", skip: 0, take: 20);

            // Takip edilmeyen "yabanci" akışta yok; en yeni olay başta.
            Assert.Equal(new[] { "ali", "veli" }, feed.Select(f => f.Username));
            Assert.All(feed, f => Assert.Equal("Test Show", f.ShowName));
        }

        [Fact]
        public async Task GetFeedAsync_CarriesEpisodeAndReviewDetail()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "ali");
            var service = new ActivityService(context);
            var episode = await context.Episodes.OrderBy(e => e.EpisodeNumber).LastAsync();

            await service.RecordWatchedAsync("ali", show.Id, episode.Id, episodeCount: 2);

            var review = new Review { UserId = "ali", ShowId = show.Id, Body = "Harika dizi", HasSpoilers = true };
            context.Reviews.Add(review);
            await context.SaveChangesAsync();
            await service.RecordReviewedAsync("ali", review.Id, show.Id, seasonNumber: null);

            var feed = await service.GetFeedAsync("ali", skip: 0, take: 20);

            var reviewed = feed.Single(f => f.Type == ActivityType.Reviewed);
            Assert.Equal("Harika dizi", reviewed.ReviewExcerpt);
            Assert.True(reviewed.ReviewHasSpoilers);

            var watched = feed.Single(f => f.Type == ActivityType.Watched);
            Assert.Equal(2, watched.EpisodeCount);
            Assert.Equal(1, watched.SeasonNumber);
            Assert.Equal(2, watched.EpisodeNumber);
            Assert.Equal("Bölüm 2", watched.EpisodeName);
        }

        [Fact]
        public async Task RatingService_WritesAndRemovesActivity()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "ali");
            var activity = new ActivityService(context);
            var ratings = new RatingService(context, activity);

            var request = new SetRatingRequest { TargetType = RatingTargetType.Show, Value = 4.5m };
            await ratings.SetRatingAsync("ali", show.TmdbId, request);

            var only = Assert.Single(context.ActivityEvents);
            Assert.Equal(4.5m, only.RatingValue);

            await ratings.RemoveRatingAsync("ali", show.TmdbId, request);
            Assert.Empty(context.ActivityEvents);
        }

        [Fact]
        public async Task ReviewService_DeletingReviewRemovesActivity()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "ali");
            var activity = new ActivityService(context);
            var reviews = new ReviewService(context, new LocalOnlyCatalogService(context), activity);

            var created = await reviews.UpsertAsync("ali", show.TmdbId, new UpsertReviewRequest { Body = "İyiydi" });
            Assert.NotNull(created);
            Assert.Single(context.ActivityEvents);

            await reviews.DeleteAsync("ali", created!.Id);
            Assert.Empty(context.ActivityEvents);
        }

        [Fact]
        public async Task EpisodeProgressService_BulkMarkWritesSingleEvent()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "ali");
            var activity = new ActivityService(context);
            var progress = new EpisodeProgressService(context, activity);

            var marked = await progress.SetSeasonWatchedAsync("ali", show.TmdbId, seasonNumber: 1, watched: true);

            Assert.Equal(2, marked);
            var only = Assert.Single(context.ActivityEvents);
            Assert.Equal(ActivityType.Watched, only.Type);
            Assert.Equal(2, only.EpisodeCount);

            await progress.SetSeasonWatchedAsync("ali", show.TmdbId, seasonNumber: 1, watched: false);
            Assert.Empty(context.ActivityEvents);
        }
    }
}
