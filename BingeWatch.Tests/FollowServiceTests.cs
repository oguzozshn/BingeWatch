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
            var service = new FollowService(context, new ActivityService(context));

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
            var service = new FollowService(context, new ActivityService(context));

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
            var service = new FollowService(context, new ActivityService(context));

            var result = await service.FollowAsync("ali", "ali");

            Assert.Equal(FollowResult.Self, result);
            Assert.Empty(context.Follows);
        }

        [Fact]
        public async Task FollowAsync_UnknownTargetReturnsNotFound()
        {
            using var context = CreateContext();
            await SeedUsersAsync(context, "ali");
            var service = new FollowService(context, new ActivityService(context));

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
            var service = new FollowService(context, new ActivityService(context));

            var result = await service.FollowAsync("ali", "gizli");

            Assert.Equal(FollowResult.TargetNotFound, result);
        }

        [Fact]
        public async Task UnfollowAsync_RemovesRelationAndIsIdempotent()
        {
            using var context = CreateContext();
            await SeedUsersAsync(context, "ali", "veli");
            var service = new FollowService(context, new ActivityService(context));
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
            var service = new FollowService(context, new ActivityService(context));

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
            var service = new FollowService(context, new ActivityService(context));
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
            var service = new FollowService(context, new ActivityService(context));

            Assert.Null(await service.GetFollowersAsync("gizli", "ali"));
            Assert.NotNull(await service.GetFollowersAsync("gizli", "gizli"));
        }
    }
}
