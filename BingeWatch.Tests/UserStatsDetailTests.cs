using BingeWatch.API.Data;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BingeWatch.Tests
{
    public class UserStatsDetailTests
    {
        private static BingeOnDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<BingeOnDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new BingeOnDbContext(options);
        }

        /// <summary>
        /// İki dizi: "Drama Dizi" (drama, 3 bölüm izlendi, biri süresiz) ve
        /// "Kara Komedi" (drama + komedi, 1 bölüm izlendi).
        /// </summary>
        private static async Task SeedAsync(BingeOnDbContext context, bool isPrivate = false)
        {
            context.Users.Add(new AppUser
            {
                Id = "ali",
                UserName = "ali",
                NormalizedUserName = "ALI",
                DisplayName = "Ali",
                IsPrivate = isPrivate
            });

            var drama = new Genre { Id = 18, Name = "Drama" };
            var comedy = new Genre { Id = 35, Name = "Comedy" };
            context.Genres.AddRange(drama, comedy);

            var dramaShow = new Show
            {
                TmdbId = 1,
                Name = "Drama Dizi",
                LastSyncedAt = DateTime.UtcNow,
                Genres = { drama }
            };
            var mixedShow = new Show
            {
                TmdbId = 2,
                Name = "Kara Komedi",
                LastSyncedAt = DateTime.UtcNow,
                Genres = { drama, comedy }
            };
            context.Shows.AddRange(dramaShow, mixedShow);
            await context.SaveChangesAsync();

            var dramaSeason = new Season { ShowId = dramaShow.Id, SeasonNumber = 1, EpisodeCount = 3 };
            var mixedSeason = new Season { ShowId = mixedShow.Id, SeasonNumber = 1, EpisodeCount = 1 };
            context.Seasons.AddRange(dramaSeason, mixedSeason);
            await context.SaveChangesAsync();

            var e1 = new Episode { SeasonId = dramaSeason.Id, EpisodeNumber = 1, Name = "B1", Runtime = 50 };
            var e2 = new Episode { SeasonId = dramaSeason.Id, EpisodeNumber = 2, Name = "B2", Runtime = 50 };
            // Süresi bilinmeyen bölüm: toplam süreye girmemeli ama sayıya girmeli.
            var e3 = new Episode { SeasonId = dramaSeason.Id, EpisodeNumber = 3, Name = "B3", Runtime = null };
            var e4 = new Episode { SeasonId = mixedSeason.Id, EpisodeNumber = 1, Name = "K1", Runtime = 30 };
            context.Episodes.AddRange(e1, e2, e3, e4);

            context.UserShows.AddRange(
                new UserShow { UserId = "ali", ShowId = dramaShow.Id, Status = WatchStatus.Watching },
                new UserShow { UserId = "ali", ShowId = mixedShow.Id, Status = WatchStatus.Completed });

            await context.SaveChangesAsync();

            context.WatchedEpisodes.AddRange(
                new WatchedEpisode { UserId = "ali", EpisodeId = e1.Id, WatchedAt = new DateTime(2025, 3, 1) },
                new WatchedEpisode { UserId = "ali", EpisodeId = e2.Id, WatchedAt = new DateTime(2026, 1, 5) },
                new WatchedEpisode { UserId = "ali", EpisodeId = e3.Id, WatchedAt = new DateTime(2026, 1, 6) },
                new WatchedEpisode { UserId = "ali", EpisodeId = e4.Id, WatchedAt = new DateTime(2026, 2, 2) });

            context.Ratings.AddRange(
                new Rating { UserId = "ali", TargetType = RatingTargetType.Show, TargetId = dramaShow.Id, Value = 4.5m },
                new Rating { UserId = "ali", TargetType = RatingTargetType.Show, TargetId = mixedShow.Id, Value = 3.5m },
                new Rating { UserId = "ali", TargetType = RatingTargetType.Episode, TargetId = e1.Id, Value = 4.5m });

            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task GetDetailedStatsAsync_CountsEpisodesAndRuntime()
        {
            using var context = CreateContext();
            await SeedAsync(context);

            var stats = await new UserStatsService(context).GetDetailedStatsAsync("ali", "ali");

            Assert.Equal(4, stats!.WatchedEpisodeCount);
            // 50 + 50 + 30; süresi bilinmeyen bölüm toplama girmez.
            Assert.Equal(130, stats.TotalMinutes);
            Assert.Equal(1, stats.EpisodesWithoutRuntime);
        }

        /// <summary>
        /// Yeniden izleme harcanan zamana girer ama "kaç farklı bölüm izledin"
        /// sayısını şişirmez.
        /// </summary>
        [Fact]
        public async Task GetDetailedStatsAsync_RewatchAddsMinutesButNotEpisodeCount()
        {
            using var context = CreateContext();
            await SeedAsync(context);

            var first = await context.WatchedEpisodes
                .Include(w => w.Episode)
                .FirstAsync(w => w.Episode!.Runtime == 50);

            context.WatchedEpisodes.Add(new WatchedEpisode
            {
                UserId = "ali",
                EpisodeId = first.EpisodeId,
                WatchedAt = new DateTime(2026, 5, 1),
                RewatchNo = 1
            });
            await context.SaveChangesAsync();

            var stats = await new UserStatsService(context).GetDetailedStatsAsync("ali", "ali");

            Assert.Equal(4, stats!.WatchedEpisodeCount);
            Assert.Equal(1, stats.RewatchCount);
            Assert.Equal(180, stats.TotalMinutes);
        }

        [Fact]
        public async Task GetDetailedStatsAsync_BreaksDownStatuses()
        {
            using var context = CreateContext();
            await SeedAsync(context);

            var stats = await new UserStatsService(context).GetDetailedStatsAsync("ali", "ali");

            Assert.Equal(2, stats!.ShowCount);
            Assert.Equal(1, stats.ShowsWatchingCount);
            Assert.Equal(1, stats.ShowsCompletedCount);
            Assert.Equal(0, stats.ShowsDroppedCount);
        }

        [Fact]
        public async Task GetDetailedStatsAsync_GenreDistributionCountsShowEpisodesPerGenre()
        {
            using var context = CreateContext();
            await SeedAsync(context);

            var stats = await new UserStatsService(context).GetDetailedStatsAsync("ali", "ali");

            var drama = stats!.Genres.Single(g => g.GenreId == 18);
            var comedy = stats.Genres.Single(g => g.GenreId == 35);

            // Drama iki dizide de var: 3 + 1 bölüm.
            Assert.Equal(4, drama.EpisodeCount);
            Assert.Equal(2, drama.ShowCount);
            // Komedi yalnızca karma dizide.
            Assert.Equal(1, comedy.EpisodeCount);
            Assert.Equal(1, comedy.ShowCount);
            // En çok izlenen tür başta.
            Assert.Equal(18, stats.Genres[0].GenreId);
        }

        [Fact]
        public async Task GetDetailedStatsAsync_YearlySplitsEpisodesAndMinutes()
        {
            using var context = CreateContext();
            await SeedAsync(context);

            var stats = await new UserStatsService(context).GetDetailedStatsAsync("ali", "ali");

            Assert.Equal(new[] { 2025, 2026 }, stats!.Yearly.Select(y => y.Year));
            Assert.Equal(1, stats.Yearly[0].EpisodeCount);
            Assert.Equal(50, stats.Yearly[0].Minutes);
            Assert.Equal(3, stats.Yearly[1].EpisodeCount);
            // 2026'da 50 + (süresiz) + 30.
            Assert.Equal(80, stats.Yearly[1].Minutes);
        }

        [Fact]
        public async Task GetDetailedStatsAsync_TopShowsOrderedByEpisodeCount()
        {
            using var context = CreateContext();
            await SeedAsync(context);

            var stats = await new UserStatsService(context).GetDetailedStatsAsync("ali", "ali");

            Assert.Equal(2, stats!.TopShows.Count);
            Assert.Equal("Drama Dizi", stats.TopShows[0].Name);
            Assert.Equal(3, stats.TopShows[0].EpisodeCount);
            Assert.Equal(100, stats.TopShows[0].Minutes);
        }

        [Fact]
        public async Task GetDetailedStatsAsync_RatingDistributionHasTenBucketsAndAverage()
        {
            using var context = CreateContext();
            await SeedAsync(context);

            var stats = await new UserStatsService(context).GetDetailedStatsAsync("ali", "ali");

            Assert.Equal(10, stats!.RatingDistribution.Count);
            Assert.Equal(0.5m, stats.RatingDistribution[0].Value);
            Assert.Equal(5m, stats.RatingDistribution[9].Value);
            // 4,5 kovasında dizi ve bölüm puanı birlikte iki kayıt var.
            Assert.Equal(2, stats.RatingDistribution.Single(b => b.Value == 4.5m).Count);
            Assert.Equal(3, stats.RatingCount);
            // Ortalama yalnızca dizi seviyesindeki puanlardan: (4,5 + 3,5) / 2.
            Assert.Equal(4.0, stats.AverageRating);
        }

        [Fact]
        public async Task GetDetailedStatsAsync_PrivateProfileHiddenFromOthers()
        {
            using var context = CreateContext();
            await SeedAsync(context, isPrivate: true);
            var service = new UserStatsService(context);

            Assert.NotNull(await service.GetDetailedStatsAsync("ali", "ali"));
            Assert.Null(await service.GetDetailedStatsAsync("ali", "veli"));
            Assert.Null(await service.GetDetailedStatsAsync("ali", null));
        }

        [Fact]
        public async Task GetDetailedStatsAsync_UnknownUserReturnsNull()
        {
            using var context = CreateContext();
            await SeedAsync(context);

            Assert.Null(await new UserStatsService(context).GetDetailedStatsAsync("yok", null));
        }
    }
}
