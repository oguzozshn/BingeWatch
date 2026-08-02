using BingeWatch.API.Data;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BingeWatch.Tests
{
    public class EpisodeProgressServiceTests
    {
        private static BingeOnDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<BingeOnDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new BingeOnDbContext(options);
        }

        /// <summary>2 sezon, sezon başına 2 bölüm olan bir dizi kurar; tamamı yayınlanmış.</summary>
        private static async Task<(Show show, Episode[] episodes)> SeedShowAsync(BingeOnDbContext context, string userId, WatchStatus status = WatchStatus.PlanToWatch)
        {
            var show = new Show { TmdbId = 1, Name = "Test Show", LastSyncedAt = DateTime.UtcNow };
            context.Shows.Add(show);
            await context.SaveChangesAsync();

            var season1 = new Season { ShowId = show.Id, SeasonNumber = 1, EpisodeCount = 2 };
            var season2 = new Season { ShowId = show.Id, SeasonNumber = 2, EpisodeCount = 2 };
            context.Seasons.AddRange(season1, season2);
            await context.SaveChangesAsync();

            var aired = DateTime.UtcNow.AddDays(-30);
            var e1 = new Episode { SeasonId = season1.Id, EpisodeNumber = 1, Name = "S1E1", AirDate = aired };
            var e2 = new Episode { SeasonId = season1.Id, EpisodeNumber = 2, Name = "S1E2", AirDate = aired };
            var e3 = new Episode { SeasonId = season2.Id, EpisodeNumber = 1, Name = "S2E1", AirDate = aired };
            var e4 = new Episode { SeasonId = season2.Id, EpisodeNumber = 2, Name = "S2E2", AirDate = aired };
            context.Episodes.AddRange(e1, e2, e3, e4);
            await context.SaveChangesAsync();

            context.UserShows.Add(new UserShow { UserId = userId, ShowId = show.Id, Status = status });
            await context.SaveChangesAsync();

            // Include ile Season'ı geri yükle ki testler navigation'a güvenebilsin.
            var reloaded = await context.Shows.Include(s => s.Seasons).ThenInclude(se => se.Episodes)
                .FirstAsync(s => s.Id == show.Id);

            return (reloaded, new[] { e1, e2, e3, e4 });
        }

        [Fact]
        public async Task SetEpisodeWatchedAsync_MarksSingleEpisode_AndFlipsStatusToWatching()
        {
            using var context = CreateContext();
            var (_, episodes) = await SeedShowAsync(context, "user1");
            var service = new EpisodeProgressService(context, new ActivityService(context));

            var success = await service.SetEpisodeWatchedAsync("user1", episodes[0].Id, watched: true);

            Assert.True(success);
            var userShow = await context.UserShows.SingleAsync();
            Assert.Equal(WatchStatus.Watching, userShow.Status);
            Assert.NotNull(userShow.StartedAt);
        }

        [Fact]
        public async Task SetEpisodeWatchedAsync_Unmark_RemovesWatchedRow()
        {
            using var context = CreateContext();
            var (_, episodes) = await SeedShowAsync(context, "user1");
            var service = new EpisodeProgressService(context, new ActivityService(context));
            await service.SetEpisodeWatchedAsync("user1", episodes[0].Id, watched: true);

            await service.SetEpisodeWatchedAsync("user1", episodes[0].Id, watched: false);

            Assert.Empty(context.WatchedEpisodes);
        }

        [Fact]
        public async Task SetSeasonWatchedAsync_MarksOnlyEpisodesInThatSeason()
        {
            using var context = CreateContext();
            await SeedShowAsync(context, "user1");
            var service = new EpisodeProgressService(context, new ActivityService(context));

            var affected = await service.SetSeasonWatchedAsync("user1", showTmdbId: 1, seasonNumber: 1, watched: true);

            Assert.Equal(2, affected);
            Assert.Equal(2, context.WatchedEpisodes.Count());
        }

        [Fact]
        public async Task SetWatchedUpToAsync_MarksEverythingUpToAndIncludingTarget_AcrossSeasons()
        {
            using var context = CreateContext();
            var (_, episodes) = await SeedShowAsync(context, "user1");
            var service = new EpisodeProgressService(context, new ActivityService(context));
            var s2e1 = episodes[2]; // Season 2, Episode 1

            var affected = await service.SetWatchedUpToAsync("user1", showTmdbId: 1, episodeId: s2e1.Id);

            // S1E1, S1E2, S2E1 izlendi sayilmali; S2E2 henuz degil.
            Assert.Equal(3, affected);
            Assert.Equal(3, context.WatchedEpisodes.Count());
            Assert.False(context.WatchedEpisodes.Any(w => w.EpisodeId == episodes[3].Id));
        }

        [Fact]
        public async Task GetShowProgressAsync_ReportsNextUnwatchedEpisode()
        {
            using var context = CreateContext();
            var (_, episodes) = await SeedShowAsync(context, "user1");
            var service = new EpisodeProgressService(context, new ActivityService(context));
            await service.SetEpisodeWatchedAsync("user1", episodes[0].Id, watched: true);

            var progress = await service.GetShowProgressAsync("user1", showTmdbId: 1);

            Assert.NotNull(progress);
            Assert.Equal(4, progress!.TotalEpisodes);
            Assert.Equal(1, progress.WatchedEpisodes);
            Assert.Equal(1, progress.NextEpisode!.SeasonNumber);
            Assert.Equal(2, progress.NextEpisode.EpisodeNumber);
        }

        [Fact]
        public async Task WatchingAllAiredEpisodes_MarksShowCompleted()
        {
            using var context = CreateContext();
            var (_, episodes) = await SeedShowAsync(context, "user1");
            var service = new EpisodeProgressService(context, new ActivityService(context));

            foreach (var ep in episodes)
                await service.SetEpisodeWatchedAsync("user1", ep.Id, watched: true);

            var userShow = await context.UserShows.SingleAsync();
            Assert.Equal(WatchStatus.Completed, userShow.Status);
            Assert.NotNull(userShow.CompletedAt);

            var progress = await service.GetShowProgressAsync("user1", 1);
            Assert.Null(progress!.NextEpisode);
        }

        [Fact]
        public async Task UnmarkingAnEpisode_RevertsCompletedShowBackToWatching()
        {
            using var context = CreateContext();
            var (_, episodes) = await SeedShowAsync(context, "user1");
            var service = new EpisodeProgressService(context, new ActivityService(context));
            foreach (var ep in episodes)
                await service.SetEpisodeWatchedAsync("user1", ep.Id, watched: true);

            await service.SetEpisodeWatchedAsync("user1", episodes[0].Id, watched: false);

            var userShow = await context.UserShows.SingleAsync();
            Assert.Equal(WatchStatus.Watching, userShow.Status);
            Assert.Null(userShow.CompletedAt);
        }

        [Fact]
        public async Task SetEpisodeWatchedAsync_DoesNotResurrectDroppedStatus()
        {
            using var context = CreateContext();
            var (_, episodes) = await SeedShowAsync(context, "user1", WatchStatus.Dropped);
            var service = new EpisodeProgressService(context, new ActivityService(context));

            await service.SetEpisodeWatchedAsync("user1", episodes[0].Id, watched: true);

            var userShow = await context.UserShows.SingleAsync();
            Assert.Equal(WatchStatus.Dropped, userShow.Status);
        }

        [Fact]
        public async Task GetNextUpAsync_ExcludesCompletedAndDroppedShows()
        {
            using var context = CreateContext();
            await SeedShowAsync(context, "user1", WatchStatus.Completed);
            var service = new EpisodeProgressService(context, new ActivityService(context));

            var nextUp = await service.GetNextUpAsync("user1");

            Assert.Empty(nextUp);
        }

        [Fact]
        public async Task GetUpcomingEpisodesAsync_OnlyReturnsFutureEpisodesWithinWindow()
        {
            using var context = CreateContext();
            var show = new Show { TmdbId = 2, Name = "Upcoming Show", LastSyncedAt = DateTime.UtcNow };
            context.Shows.Add(show);
            await context.SaveChangesAsync();
            var season = new Season { ShowId = show.Id, SeasonNumber = 1 };
            context.Seasons.Add(season);
            await context.SaveChangesAsync();
            context.Episodes.AddRange(
                new Episode { SeasonId = season.Id, EpisodeNumber = 1, Name = "Soon", AirDate = DateTime.UtcNow.AddDays(5) },
                new Episode { SeasonId = season.Id, EpisodeNumber = 2, Name = "TooFar", AirDate = DateTime.UtcNow.AddDays(60) },
                new Episode { SeasonId = season.Id, EpisodeNumber = 3, Name = "Past", AirDate = DateTime.UtcNow.AddDays(-5) });
            await context.SaveChangesAsync();
            context.UserShows.Add(new UserShow { UserId = "user1", ShowId = show.Id, Status = WatchStatus.Watching });
            await context.SaveChangesAsync();
            var service = new EpisodeProgressService(context, new ActivityService(context));

            var upcoming = await service.GetUpcomingEpisodesAsync("user1", daysAhead: 30);

            Assert.Single(upcoming);
            Assert.Equal("Soon", upcoming[0].EpisodeName);
        }

        [Fact]
        public async Task AddRewatchAsync_RequiresFirstWatch()
        {
            using var context = CreateContext();
            var (_, episodes) = await SeedShowAsync(context, "user1");
            var service = new EpisodeProgressService(context, new ActivityService(context));

            var result = await service.AddRewatchAsync("user1", episodes[0].Id);

            Assert.Null(result);
            Assert.Empty(context.WatchedEpisodes);
        }

        [Fact]
        public async Task AddRewatchAsync_IncrementsRewatchNo()
        {
            using var context = CreateContext();
            var (_, episodes) = await SeedShowAsync(context, "user1");
            var service = new EpisodeProgressService(context, new ActivityService(context));
            await service.SetEpisodeWatchedAsync("user1", episodes[0].Id, watched: true);

            Assert.Equal(1, await service.AddRewatchAsync("user1", episodes[0].Id));
            Assert.Equal(2, await service.AddRewatchAsync("user1", episodes[0].Id));

            Assert.Equal(2, await service.GetRewatchCountAsync("user1", episodes[0].Id));
            Assert.Equal(3, await context.WatchedEpisodes.CountAsync());
        }

        /// <summary>
        /// Rewatch'in asıl sözleşmesi: ilerleme modeline dokunmaz. Bitmiş bir dizi
        /// yeniden izlenince "Bitirdim"de kalmalı ve "Sırada ne var"a düşmemeli.
        /// </summary>
        [Fact]
        public async Task AddRewatchAsync_DoesNotAffectStatusOrProgress()
        {
            using var context = CreateContext();
            var (show, episodes) = await SeedShowAsync(context, "user1");
            var service = new EpisodeProgressService(context, new ActivityService(context));

            foreach (var episode in episodes)
                await service.SetEpisodeWatchedAsync("user1", episode.Id, watched: true);

            var completed = await context.UserShows.SingleAsync();
            Assert.Equal(WatchStatus.Completed, completed.Status);

            await service.AddRewatchAsync("user1", episodes[0].Id);

            var afterRewatch = await context.UserShows.SingleAsync();
            Assert.Equal(WatchStatus.Completed, afterRewatch.Status);

            var progress = await service.GetShowProgressAsync("user1", show.TmdbId);
            Assert.Equal(4, progress!.WatchedEpisodes);
            Assert.Equal(4, progress.TotalEpisodes);
            Assert.Null(progress.NextEpisode);

            Assert.Empty(await service.GetNextUpAsync("user1"));
        }

        [Fact]
        public async Task RemoveLastRewatchAsync_RemovesOnlyLatestAndKeepsFirstWatch()
        {
            using var context = CreateContext();
            var (_, episodes) = await SeedShowAsync(context, "user1");
            var service = new EpisodeProgressService(context, new ActivityService(context));
            await service.SetEpisodeWatchedAsync("user1", episodes[0].Id, watched: true);
            await service.AddRewatchAsync("user1", episodes[0].Id);
            await service.AddRewatchAsync("user1", episodes[0].Id);

            Assert.Equal(1, await service.RemoveLastRewatchAsync("user1", episodes[0].Id));
            Assert.Equal(0, await service.RemoveLastRewatchAsync("user1", episodes[0].Id));
            Assert.Null(await service.RemoveLastRewatchAsync("user1", episodes[0].Id));

            var remaining = await context.WatchedEpisodes.SingleAsync();
            Assert.Equal(0, remaining.RewatchNo);
        }

        /// <summary>
        /// "İzlemedim" demek tüm geçişleri kapsar; rewatch satırları kalsaydı
        /// izlenmemiş görünen bir bölüm istatistikte süre saymaya devam ederdi.
        /// </summary>
        [Fact]
        public async Task UnmarkingEpisode_AlsoRemovesRewatches()
        {
            using var context = CreateContext();
            var (_, episodes) = await SeedShowAsync(context, "user1");
            var service = new EpisodeProgressService(context, new ActivityService(context));
            await service.SetEpisodeWatchedAsync("user1", episodes[0].Id, watched: true);
            await service.AddRewatchAsync("user1", episodes[0].Id);

            await service.SetEpisodeWatchedAsync("user1", episodes[0].Id, watched: false);

            Assert.Empty(context.WatchedEpisodes);
        }
    }
}
