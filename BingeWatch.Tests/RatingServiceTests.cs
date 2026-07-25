using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BingeWatch.Tests
{
    public class RatingServiceTests
    {
        private static BingeOnDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<BingeOnDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new BingeOnDbContext(options);
        }

        /// <summary>TmdbId 1: tek sezon, iki bölüm. TmdbId 2: ilgisiz ikinci dizi.</summary>
        private static async Task<(Show show, Season season, Episode[] episodes, Episode foreignEpisode)>
            SeedAsync(BingeOnDbContext context)
        {
            var show = new Show { TmdbId = 1, Name = "Test Show", LastSyncedAt = DateTime.UtcNow };
            var other = new Show { TmdbId = 2, Name = "Other Show", LastSyncedAt = DateTime.UtcNow };
            context.Shows.AddRange(show, other);
            await context.SaveChangesAsync();

            var season = new Season { ShowId = show.Id, SeasonNumber = 1, EpisodeCount = 2 };
            var otherSeason = new Season { ShowId = other.Id, SeasonNumber = 1, EpisodeCount = 1 };
            context.Seasons.AddRange(season, otherSeason);
            await context.SaveChangesAsync();

            var e1 = new Episode { SeasonId = season.Id, EpisodeNumber = 1, Name = "S1E1" };
            var e2 = new Episode { SeasonId = season.Id, EpisodeNumber = 2, Name = "S1E2" };
            var foreign = new Episode { SeasonId = otherSeason.Id, EpisodeNumber = 1, Name = "Foreign" };
            context.Episodes.AddRange(e1, e2, foreign);
            await context.SaveChangesAsync();

            return (show, season, new[] { e1, e2 }, foreign);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(0.3)]
        [InlineData(5.5)]
        [InlineData(-1)]
        public async Task SetRatingAsync_RejectsValuesOutsideHalfStarScale(double value)
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = new RatingService(context);

            var result = await service.SetRatingAsync("user1", 1, new SetRatingRequest
            {
                TargetType = RatingTargetType.Show,
                Value = (decimal)value
            });

            Assert.Null(result);
            Assert.Empty(context.Ratings);
        }

        [Fact]
        public async Task SetRatingAsync_SecondCallUpdatesInsteadOfDuplicating()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = new RatingService(context);
            var request = new SetRatingRequest { TargetType = RatingTargetType.Show, Value = 3.5m };

            await service.SetRatingAsync("user1", 1, request);
            request.Value = 4.5m;
            await service.SetRatingAsync("user1", 1, request);

            var rating = await context.Ratings.SingleAsync();
            Assert.Equal(4.5m, rating.Value);
        }

        [Fact]
        public async Task SetRatingAsync_RejectsEpisodeBelongingToAnotherShow()
        {
            using var context = CreateContext();
            var (_, _, _, foreign) = await SeedAsync(context);
            var service = new RatingService(context);

            var result = await service.SetRatingAsync("user1", 1, new SetRatingRequest
            {
                TargetType = RatingTargetType.Episode,
                EpisodeId = foreign.Id,
                Value = 5m
            });

            Assert.Null(result);
            Assert.Empty(context.Ratings);
        }

        [Fact]
        public async Task SetRatingAsync_RejectsUnknownSeasonNumber()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = new RatingService(context);

            var result = await service.SetRatingAsync("user1", 1, new SetRatingRequest
            {
                TargetType = RatingTargetType.Season,
                SeasonNumber = 9,
                Value = 4m
            });

            Assert.Null(result);
        }

        [Fact]
        public async Task GetUserRatingsForShowAsync_GroupsAllThreeLevels()
        {
            using var context = CreateContext();
            var (_, _, episodes, _) = await SeedAsync(context);
            var service = new RatingService(context);
            await service.SetRatingAsync("user1", 1, new SetRatingRequest { TargetType = RatingTargetType.Show, Value = 4m });
            await service.SetRatingAsync("user1", 1, new SetRatingRequest { TargetType = RatingTargetType.Season, SeasonNumber = 1, Value = 3.5m });
            await service.SetRatingAsync("user1", 1, new SetRatingRequest { TargetType = RatingTargetType.Episode, EpisodeId = episodes[0].Id, Value = 5m });

            var ratings = await service.GetUserRatingsForShowAsync("user1", 1);

            Assert.NotNull(ratings);
            Assert.Equal(4m, ratings!.ShowRating);
            Assert.Equal(3.5m, ratings.SeasonRatings[1]);
            Assert.Equal(5m, ratings.EpisodeRatings[episodes[0].Id]);
        }

        [Fact]
        public async Task GetUserRatingsForShowAsync_IgnoresOtherUsersRatings()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = new RatingService(context);
            await service.SetRatingAsync("user2", 1, new SetRatingRequest { TargetType = RatingTargetType.Show, Value = 1m });

            var ratings = await service.GetUserRatingsForShowAsync("user1", 1);

            Assert.Null(ratings!.ShowRating);
        }

        [Fact]
        public async Task RemoveRatingAsync_DeletesRow_AndReportsMissingOnes()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = new RatingService(context);
            var request = new SetRatingRequest { TargetType = RatingTargetType.Show, Value = 4m };
            await service.SetRatingAsync("user1", 1, request);

            Assert.True(await service.RemoveRatingAsync("user1", 1, request));
            Assert.False(await service.RemoveRatingAsync("user1", 1, request));
            Assert.Empty(context.Ratings);
        }

        [Fact]
        public async Task GetShowSummaryAsync_AveragesShowLevelRatingsOnly()
        {
            using var context = CreateContext();
            var (_, _, episodes, _) = await SeedAsync(context);
            var service = new RatingService(context);
            await service.SetRatingAsync("user1", 1, new SetRatingRequest { TargetType = RatingTargetType.Show, Value = 4m });
            await service.SetRatingAsync("user2", 1, new SetRatingRequest { TargetType = RatingTargetType.Show, Value = 3m });
            // Bölüm puanı diziye ait ortalamayı kaydırmamalı.
            await service.SetRatingAsync("user1", 1, new SetRatingRequest { TargetType = RatingTargetType.Episode, EpisodeId = episodes[0].Id, Value = 0.5m });

            var summary = await service.GetShowSummaryAsync(1);

            Assert.Equal(2, summary!.Count);
            Assert.Equal(3.5, summary.Average);
            Assert.Equal(1, summary.Distribution["4.0"]);
            Assert.Equal(1, summary.Distribution["3.0"]);
            Assert.Equal(0, summary.Distribution["0.5"]);
        }

        [Fact]
        public async Task GetShowSummaryAsync_ReturnsAllTenBucketsEvenWhenUnrated()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = new RatingService(context);

            var summary = await service.GetShowSummaryAsync(1);

            Assert.Null(summary!.Average);
            Assert.Equal(0, summary.Count);
            Assert.Equal(10, summary.Distribution.Count);
        }
    }
}
