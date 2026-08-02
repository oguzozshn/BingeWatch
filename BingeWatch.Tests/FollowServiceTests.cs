using BingeWatch.API.Data;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BingeWatch.Tests
{
    public class FollowServiceTests
    {
        private static BingeOnDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<BingeOnDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new BingeOnDbContext(options);
        }

        private static async Task SeedUsersAsync(BingeOnDbContext context, params string[] userIds)
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

            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task FollowAsync_CreatesRelation()
        {
            using var context = CreateContext();
            await SeedUsersAsync(context, "ali", "veli");
            var service = new FollowService(context, new ActivityService(context), new NotificationService(context));

            var result = await service.FollowAsync("ali", "veli");

            Assert.Equal(FollowResult.Ok, result);
            Assert.True(await service.IsFollowingAsync("ali", "veli"));
            Assert.Equal(1, await service.GetFollowerCountAsync("veli"));
            Assert.Equal(1, await service.GetFollowingCountAsync("ali"));
        }

        [Fact]
        public async Task FollowAsync_IsIdempotent()
        {
            using var context = CreateContext();
            await SeedUsersAsync(context, "ali", "veli");
            var service = new FollowService(context, new ActivityService(context), new NotificationService(context));

            await service.FollowAsync("ali", "veli");
            var second = await service.FollowAsync("ali", "veli");

            Assert.Equal(FollowResult.Ok, second);
            Assert.Equal(1, await context.Follows.CountAsync());
        }

        [Fact]
        public async Task FollowAsync_RejectsSelf()
        {
            using var context = CreateContext();
            await SeedUsersAsync(context, "ali");
            var service = new FollowService(context, new ActivityService(context), new NotificationService(context));

            var result = await service.FollowAsync("ali", "ali");

            Assert.Equal(FollowResult.Self, result);
            Assert.Empty(context.Follows);
        }

        [Fact]
        public async Task FollowAsync_UnknownTargetReturnsNotFound()
        {
            using var context = CreateContext();
            await SeedUsersAsync(context, "ali");
            var service = new FollowService(context, new ActivityService(context), new NotificationService(context));

            var result = await service.FollowAsync("ali", "kimse");

            Assert.Equal(FollowResult.TargetNotFound, result);
        }

        [Fact]
        public async Task FollowAsync_PrivateTargetIsHidden()
        {
            using var context = CreateContext();
            await SeedUsersAsync(context, "ali", "gizli");
            var target = await context.Users.FirstAsync(u => u.Id == "gizli");
            target.IsPrivate = true;
            await context.SaveChangesAsync();
            var service = new FollowService(context, new ActivityService(context), new NotificationService(context));

            var result = await service.FollowAsync("ali", "gizli");

            Assert.Equal(FollowResult.TargetNotFound, result);
        }

        [Fact]
        public async Task UnfollowAsync_RemovesRelationAndIsIdempotent()
        {
            using var context = CreateContext();
            await SeedUsersAsync(context, "ali", "veli");
            var service = new FollowService(context, new ActivityService(context), new NotificationService(context));
            await service.FollowAsync("ali", "veli");

            Assert.Equal(FollowResult.Ok, await service.UnfollowAsync("ali", "veli"));
            Assert.Equal(FollowResult.Ok, await service.UnfollowAsync("ali", "veli"));
            Assert.Empty(context.Follows);
        }

        [Fact]
        public async Task GetFollowersAsync_NewestFirstAndMarksViewerRelation()
        {
            using var context = CreateContext();
            await SeedUsersAsync(context, "ali", "veli", "ayse");
            var service = new FollowService(context, new ActivityService(context), new NotificationService(context));

            // ayse ve veli, ali'yi takip ediyor; sıralama en yeniden eskiye.
            context.Follows.Add(new Follow
            {
                FollowerId = "veli",
                FolloweeId = "ali",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });
            context.Follows.Add(new Follow { FollowerId = "ayse", FolloweeId = "ali" });
            // Bakan kullanıcı (veli) ayse'yi de takip ediyor.
            context.Follows.Add(new Follow { FollowerId = "veli", FolloweeId = "ayse" });
            await context.SaveChangesAsync();

            var followers = await service.GetFollowersAsync("ali", "veli");

            Assert.NotNull(followers);
            Assert.Equal(new[] { "ayse", "veli" }, followers!.Select(f => f.Username));
            Assert.True(followers[0].IsFollowedByViewer);
            Assert.False(followers[0].IsViewer);
            Assert.True(followers[1].IsViewer);
        }

        [Fact]
        public async Task GetFollowingAsync_ReturnsFolloweesForAnonymousViewer()
        {
            using var context = CreateContext();
            await SeedUsersAsync(context, "ali", "veli");
            var service = new FollowService(context, new ActivityService(context), new NotificationService(context));
            await service.FollowAsync("ali", "veli");

            var following = await service.GetFollowingAsync("ali", viewerId: null);

            Assert.NotNull(following);
            var only = Assert.Single(following!);
            Assert.Equal("veli", only.Username);
            Assert.False(only.IsFollowedByViewer);
            Assert.False(only.IsViewer);
        }

        [Fact]
        public async Task GetFollowersAsync_PrivateProfileVisibleOnlyToOwner()
        {
            using var context = CreateContext();
            await SeedUsersAsync(context, "ali", "gizli");
            var target = await context.Users.FirstAsync(u => u.Id == "gizli");
            target.IsPrivate = true;
            await context.SaveChangesAsync();
            var service = new FollowService(context, new ActivityService(context), new NotificationService(context));

            Assert.Null(await service.GetFollowersAsync("gizli", "ali"));
            Assert.NotNull(await service.GetFollowersAsync("gizli", "gizli"));
        }

        private static FollowService NewService(BingeOnDbContext context) =>
            new(context, new ActivityService(context), new NotificationService(context));

        [Fact]
        public async Task SearchAsync_MatchesUsernameAndDisplayName()
        {
            using var context = CreateContext();
            await SeedUsersAsync(context, "ali", "veli");
            var veli = await context.Users.FirstAsync(u => u.Id == "veli");
            veli.DisplayName = "Veli Kaya";
            await context.SaveChangesAsync();
            var service = NewService(context);

            var byUsername = await service.SearchAsync("vel", "ali");
            Assert.Equal("veli", Assert.Single(byUsername).Username);

            var byDisplayName = await service.SearchAsync("kaya", "ali");
            Assert.Equal("veli", Assert.Single(byDisplayName).Username);
        }

        [Fact]
        public async Task SearchAsync_IsCaseInsensitive()
        {
            using var context = CreateContext();
            await SeedUsersAsync(context, "ali", "VeLi");
            var service = NewService(context);

            Assert.Single(await service.SearchAsync("veli", "ali"));
            Assert.Single(await service.SearchAsync("VELI", "ali"));
        }

        /// <summary>
        /// Gizli profil aramada hiç görünmez — sahibine bile. Görünmesi, profilin
        /// var olduğunu duyurmaktan başka işe yaramaz.
        /// </summary>
        [Fact]
        public async Task SearchAsync_ExcludesPrivateProfiles()
        {
            using var context = CreateContext();
            await SeedUsersAsync(context, "ali", "gizli");
            var target = await context.Users.FirstAsync(u => u.Id == "gizli");
            target.IsPrivate = true;
            await context.SaveChangesAsync();
            var service = NewService(context);

            Assert.Empty(await service.SearchAsync("gizli", "ali"));
            Assert.Empty(await service.SearchAsync("gizli", "gizli"));
        }

        [Fact]
        public async Task SearchAsync_ExcludesBlockedUsersInBothDirections()
        {
            using var context = CreateContext();
            await SeedUsersAsync(context, "ali", "veli");
            context.UserBlocks.Add(new UserBlock { BlockerId = "ali", BlockedId = "veli" });
            await context.SaveChangesAsync();
            var service = NewService(context);

            Assert.Empty(await service.SearchAsync("veli", "ali"));
            Assert.Empty(await service.SearchAsync("ali", "veli"));
        }

        [Fact]
        public async Task SearchAsync_ReportsFollowStateAndSelf()
        {
            using var context = CreateContext();
            await SeedUsersAsync(context, "ali", "veli");
            var service = NewService(context);
            await service.FollowAsync("ali", "veli");

            var results = await service.SearchAsync("li", "ali");

            var veli = results.Single(u => u.Username == "veli");
            Assert.True(veli.IsFollowedByViewer);
            Assert.False(veli.IsViewer);

            var self = results.Single(u => u.Username == "ali");
            Assert.True(self.IsViewer);
        }

        /// <summary>Tek harf tüm kullanıcıları dökerdi; alt sınır bilerek iki.</summary>
        [Fact]
        public async Task SearchAsync_IgnoresTooShortQuery()
        {
            using var context = CreateContext();
            await SeedUsersAsync(context, "ali", "veli");
            var service = NewService(context);

            Assert.Empty(await service.SearchAsync("a", "ali"));
            Assert.Empty(await service.SearchAsync(" ", "ali"));
        }

        [Fact]
        public async Task SearchAsync_PrefersPrefixMatches()
        {
            using var context = CreateContext();
            await SeedUsersAsync(context, "ali", "kemalist", "alper");
            var service = NewService(context);

            var results = await service.SearchAsync("al", "ali");

            // "ali" ve "alper" baştan eşleşiyor, "kemalist" içeriyor.
            Assert.Equal("kemalist", results.Last().Username);
        }
    }
}
