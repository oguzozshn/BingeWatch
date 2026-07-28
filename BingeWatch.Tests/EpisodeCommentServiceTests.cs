using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BingeWatch.Tests
{
    /// <summary>
    /// Bölüm tartışmaları (Faz 7). Testlerin çoğu tek bir iddiayı kovalıyor:
    /// <b>ipliği yalnızca bölümü izlemiş olan açar.</b> Kapı hem okumada hem
    /// yazmada geçerli ve sunucuda uygulanıyor — arayüzde gizlemek yetmez.
    /// </summary>
    public class EpisodeCommentServiceTests
    {
        private static BingeOnDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<BingeOnDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new BingeOnDbContext(options);
        }

        /// <summary>Tek sezon, iki yayınlanmış bölüm + bir yayınlanmamış bölüm.</summary>
        private static async Task<Episode[]> SeedAsync(BingeOnDbContext context, params string[] userIds)
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

            var season = new Season { ShowId = show.Id, SeasonNumber = 1, EpisodeCount = 3 };
            context.Seasons.Add(season);
            await context.SaveChangesAsync();

            var aired = DateTime.UtcNow.AddDays(-30);
            var e1 = new Episode { SeasonId = season.Id, EpisodeNumber = 1, Name = "S1E1", AirDate = aired };
            var e2 = new Episode { SeasonId = season.Id, EpisodeNumber = 2, Name = "S1E2", AirDate = aired };
            var e3 = new Episode { SeasonId = season.Id, EpisodeNumber = 3, Name = "S1E3", AirDate = DateTime.UtcNow.AddDays(30) };
            context.Episodes.AddRange(e1, e2, e3);
            await context.SaveChangesAsync();

            return new[] { e1, e2, e3 };
        }

        private static async Task MarkWatchedAsync(BingeOnDbContext context, string userId, int episodeId)
        {
            context.WatchedEpisodes.Add(new WatchedEpisode
            {
                UserId = userId,
                EpisodeId = episodeId,
                WatchedAt = DateTime.UtcNow,
                RewatchNo = 0
            });
            await context.SaveChangesAsync();
        }

        // ----- Kapı: okuma ------------------------------------------------------

        [Fact]
        public async Task GetThreadAsync_LocksThreadForUserWhoHasNotWatched()
        {
            using var context = CreateContext();
            var episodes = await SeedAsync(context, "ali", "veli");
            var service = new EpisodeCommentService(context);

            await MarkWatchedAsync(context, "ali", episodes[0].Id);
            await service.AddAsync("ali", episodes[0].Id, new AddEpisodeCommentRequest { Body = "Finali efsaneydi" });

            var thread = await service.GetThreadAsync(episodes[0].Id, "veli");

            Assert.NotNull(thread);
            Assert.True(thread!.Locked);
            // Yorum sayısı bile sızmamalı: "burada 40 yorum var" tek başına
            // bölüm hakkında bir şey söyler.
            Assert.Empty(thread.Comments);
        }

        [Fact]
        public async Task GetThreadAsync_OpensThreadForUserWhoWatched()
        {
            using var context = CreateContext();
            var episodes = await SeedAsync(context, "ali", "veli");
            var service = new EpisodeCommentService(context);

            await MarkWatchedAsync(context, "ali", episodes[0].Id);
            await service.AddAsync("ali", episodes[0].Id, new AddEpisodeCommentRequest { Body = "Finali efsaneydi" });
            await MarkWatchedAsync(context, "veli", episodes[0].Id);

            var thread = await service.GetThreadAsync(episodes[0].Id, "veli");

            Assert.NotNull(thread);
            Assert.False(thread!.Locked);
            Assert.Single(thread.Comments);
            Assert.Equal("Finali efsaneydi", thread.Comments[0].Body);
        }

        [Fact]
        public async Task GetThreadAsync_LocksThreadForAnonymousViewer()
        {
            using var context = CreateContext();
            var episodes = await SeedAsync(context, "ali");
            var service = new EpisodeCommentService(context);

            var thread = await service.GetThreadAsync(episodes[0].Id, viewerId: null);

            Assert.NotNull(thread);
            Assert.True(thread!.Locked);
        }

        [Fact]
        public async Task GetThreadAsync_GateIsPerEpisode_NotPerShow()
        {
            using var context = CreateContext();
            var episodes = await SeedAsync(context, "ali");
            var service = new EpisodeCommentService(context);

            // Birinci bölümü izlemek ikinci bölümün ipliğini açmamalı;
            // spoiler tam olarak orada duruyor.
            await MarkWatchedAsync(context, "ali", episodes[0].Id);

            Assert.False((await service.GetThreadAsync(episodes[0].Id, "ali"))!.Locked);
            Assert.True((await service.GetThreadAsync(episodes[1].Id, "ali"))!.Locked);
        }

        [Fact]
        public async Task GetThreadAsync_MarksUnairedEpisode_SoUiCanExplainWhy()
        {
            using var context = CreateContext();
            var episodes = await SeedAsync(context, "ali");
            var service = new EpisodeCommentService(context);

            var thread = await service.GetThreadAsync(episodes[2].Id, "ali");

            Assert.NotNull(thread);
            Assert.True(thread!.Locked);
            Assert.True(thread.Unaired);
        }

        [Fact]
        public async Task GetThreadAsync_ReturnsNullForUnknownEpisode()
        {
            using var context = CreateContext();
            await SeedAsync(context, "ali");
            var service = new EpisodeCommentService(context);

            Assert.Null(await service.GetThreadAsync(9999, "ali"));
        }

        // ----- Kapı: yazma ------------------------------------------------------

        [Fact]
        public async Task AddAsync_RejectsCommentFromUserWhoHasNotWatched()
        {
            using var context = CreateContext();
            var episodes = await SeedAsync(context, "ali");
            var service = new EpisodeCommentService(context);

            var comment = await service.AddAsync("ali", episodes[0].Id,
                new AddEpisodeCommentRequest { Body = "Spoiler doluyum" });

            Assert.Null(comment);
            Assert.Empty(context.EpisodeComments);
        }

        [Fact]
        public async Task AddAsync_RejectsEmptyBody()
        {
            using var context = CreateContext();
            var episodes = await SeedAsync(context, "ali");
            var service = new EpisodeCommentService(context);
            await MarkWatchedAsync(context, "ali", episodes[0].Id);

            Assert.Null(await service.AddAsync("ali", episodes[0].Id,
                new AddEpisodeCommentRequest { Body = "   " }));
            Assert.Empty(context.EpisodeComments);
        }

        [Fact]
        public async Task AddAsync_WritesNoActivityOrNotification()
        {
            using var context = CreateContext();
            var episodes = await SeedAsync(context, "ali");
            var service = new EpisodeCommentService(context);
            await MarkWatchedAsync(context, "ali", episodes[0].Id);

            await service.AddAsync("ali", episodes[0].Id, new AddEpisodeCommentRequest { Body = "İyiydi" });

            // İpliğin yayılmaması özelliğin varlık şartı: ROADMAP §3 bölüm bazlı
            // incelemeyi tam da akışı spoiler'a boğduğu için reddetmişti.
            Assert.Empty(context.ActivityEvents);
            Assert.Empty(context.Notifications);
        }

        // ----- Engelleme ve silme -----------------------------------------------

        [Fact]
        public async Task GetThreadAsync_HidesCommentsFromBlockedUsers()
        {
            using var context = CreateContext();
            var episodes = await SeedAsync(context, "ali", "veli");
            var service = new EpisodeCommentService(context);

            await MarkWatchedAsync(context, "ali", episodes[0].Id);
            await MarkWatchedAsync(context, "veli", episodes[0].Id);
            await service.AddAsync("veli", episodes[0].Id, new AddEpisodeCommentRequest { Body = "Görünmemeli" });

            await new BlockService(context).BlockAsync("ali", "veli");

            var thread = await service.GetThreadAsync(episodes[0].Id, "ali");

            Assert.False(thread!.Locked);
            Assert.Empty(thread.Comments);
        }

        [Fact]
        public async Task DeleteAsync_OnlyAuthorCanDelete()
        {
            using var context = CreateContext();
            var episodes = await SeedAsync(context, "ali", "veli");
            var service = new EpisodeCommentService(context);

            await MarkWatchedAsync(context, "ali", episodes[0].Id);
            var comment = await service.AddAsync("ali", episodes[0].Id,
                new AddEpisodeCommentRequest { Body = "Benim yorumum" });

            // İnceleme yorumundaki "inceleme sahibi de silebilir" kuralının
            // burada karşılığı yok; bölümün sahibi diye biri yok.
            Assert.False(await service.DeleteAsync("veli", comment!.Id));
            Assert.True(await service.DeleteAsync("ali", comment.Id));
            Assert.Empty(context.EpisodeComments);
        }

        [Fact]
        public async Task UnmarkingEpisode_KeepsCommentButClosesThreadForAuthor()
        {
            using var context = CreateContext();
            var episodes = await SeedAsync(context, "ali");
            var service = new EpisodeCommentService(context);
            var progress = new EpisodeProgressService(context, new ActivityService(context));

            await progress.SetEpisodeWatchedAsync("ali", episodes[0].Id, watched: true);
            await service.AddAsync("ali", episodes[0].Id, new AddEpisodeCommentRequest { Body = "Yazdım" });

            await progress.SetEpisodeWatchedAsync("ali", episodes[0].Id, watched: false);

            // Yorum duruyor — işareti kaldırmak veri silmemeli. Ama iplik kapandı.
            Assert.Single(context.EpisodeComments);
            Assert.True((await service.GetThreadAsync(episodes[0].Id, "ali"))!.Locked);
        }
    }
}
