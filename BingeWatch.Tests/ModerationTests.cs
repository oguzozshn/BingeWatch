using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BingeWatch.Tests
{
    /// <summary>Engelleme ve içerik bildirimi (Faz 6.1).</summary>
    public class ModerationTests
    {
        private static BingeOnDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<BingeOnDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new BingeOnDbContext(options);
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

            return show;
        }

        private static async Task<Review> SeedReviewAsync(BingeOnDbContext context, Show show, string userId)
        {
            var review = new Review { UserId = userId, ShowId = show.Id, Body = "İyiydi" };
            context.Reviews.Add(review);
            await context.SaveChangesAsync();
            return review;
        }

        // ----- Engelleme --------------------------------------------------------

        [Fact]
        public async Task BlockAsync_SeversFollowsInBothDirections()
        {
            using var context = CreateContext();
            await SeedAsync(context, "ali", "veli");
            var notifications = new NotificationService(context);
            var follows = new FollowService(context, new ActivityService(context), notifications);
            var blocks = new BlockService(context);

            await follows.FollowAsync("ali", "veli");
            await follows.FollowAsync("veli", "ali");
            Assert.Equal(2, await context.Follows.CountAsync());

            Assert.Equal(BlockResult.Ok, await blocks.BlockAsync("ali", "veli"));

            Assert.Empty(context.Follows);
            Assert.Empty(context.ActivityEvents);
            Assert.Empty(context.Notifications);
        }

        [Fact]
        public async Task BlockAsync_IsIdempotentAndRejectsSelf()
        {
            using var context = CreateContext();
            await SeedAsync(context, "ali", "veli");
            var blocks = new BlockService(context);

            await blocks.BlockAsync("ali", "veli");
            await blocks.BlockAsync("ali", "veli");

            Assert.Equal(1, await context.UserBlocks.CountAsync());
            Assert.Equal(BlockResult.Self, await blocks.BlockAsync("ali", "ali"));
            Assert.Equal(BlockResult.TargetNotFound, await blocks.BlockAsync("ali", "yok-boyle-biri"));
        }

        [Fact]
        public async Task UnblockAsync_DoesNotRestoreFollows()
        {
            using var context = CreateContext();
            await SeedAsync(context, "ali", "veli");
            var follows = new FollowService(context, new ActivityService(context), new NotificationService(context));
            var blocks = new BlockService(context);

            await follows.FollowAsync("ali", "veli");
            await blocks.BlockAsync("ali", "veli");
            await blocks.UnblockAsync("ali", "veli");

            Assert.Empty(context.UserBlocks);
            Assert.Empty(context.Follows);
        }

        [Fact]
        public async Task Block_HidesProfileInBothDirections()
        {
            using var context = CreateContext();
            await SeedAsync(context, "ali", "veli");
            var follows = new FollowService(context, new ActivityService(context), new NotificationService(context));
            var stats = new UserStatsService(context);
            await new BlockService(context).BlockAsync("ali", "veli");

            // Engelleyen de engellenen de karşı tarafın takipçilerini ve istatistiğini göremez.
            Assert.Null(await follows.GetFollowersAsync("veli", "ali"));
            Assert.Null(await follows.GetFollowersAsync("ali", "veli"));
            Assert.Null(await stats.GetStatsAsync("veli", "ali"));
            Assert.Null(await stats.GetStatsAsync("ali", "veli"));

            // Üçüncü kişi ve sahibi etkilenmez.
            Assert.NotNull(await stats.GetStatsAsync("veli", "veli"));
        }

        [Fact]
        public async Task Block_PreventsFollowing()
        {
            using var context = CreateContext();
            await SeedAsync(context, "ali", "veli");
            var follows = new FollowService(context, new ActivityService(context), new NotificationService(context));
            await new BlockService(context).BlockAsync("ali", "veli");

            // Engellenen taraf da engelleyen taraf da yeniden takip edemez.
            Assert.Equal(FollowResult.TargetNotFound, await follows.FollowAsync("veli", "ali"));
            Assert.Equal(FollowResult.TargetNotFound, await follows.FollowAsync("ali", "veli"));
            Assert.Empty(context.Follows);
        }

        [Fact]
        public async Task Block_HidesReviewsFromFeedAndShowPage()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "yazar", "okur");
            await SeedReviewAsync(context, show, "yazar");
            var reviews = new ReviewService(context, new LocalOnlyCatalog(context),
                new ActivityService(context), new NotificationService(context));

            Assert.Single(await reviews.GetForShowAsync(show.TmdbId, null, "okur"));

            await new BlockService(context).BlockAsync("okur", "yazar");

            Assert.Empty(await reviews.GetForShowAsync(show.TmdbId, null, "okur"));
            Assert.Empty(await reviews.GetFeedAsync(0, 20, ReviewSort.Newest, "okur"));
            // Anonim ziyaretçi ve yazarın kendisi etkilenmez.
            Assert.Single(await reviews.GetForShowAsync(show.TmdbId));
        }

        [Fact]
        public async Task Block_PreventsLikingAndCommenting()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "yazar", "okur");
            var review = await SeedReviewAsync(context, show, "yazar");
            var interactions = new ReviewInteractionService(context, new NotificationService(context));

            await new BlockService(context).BlockAsync("yazar", "okur");

            Assert.Null(await interactions.LikeAsync("okur", review.Id));
            Assert.Null(await interactions.GetCommentsAsync(review.Id, "okur"));
            Assert.Null(await interactions.AddCommentAsync("okur", review.Id,
                new AddCommentRequest { Body = "Merhaba" }));
            Assert.Empty(context.ReviewLikes);
            Assert.Empty(context.ReviewComments);
        }

        [Fact]
        public async Task Block_HidesCommentsFromThread()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "yazar", "okur", "kaba");
            var review = await SeedReviewAsync(context, show, "yazar");
            var interactions = new ReviewInteractionService(context, new NotificationService(context));

            await interactions.AddCommentAsync("kaba", review.Id, new AddCommentRequest { Body = "Kötü yorum" });
            await interactions.AddCommentAsync("okur", review.Id, new AddCommentRequest { Body = "İyi yorum" });

            await new BlockService(context).BlockAsync("okur", "kaba");

            var thread = await interactions.GetCommentsAsync(review.Id, "okur");

            Assert.Equal("İyi yorum", Assert.Single(thread!).Body);
            // Engellemeyen okuyucu ikisini de görür.
            Assert.Equal(2, (await interactions.GetCommentsAsync(review.Id, "yazar"))!.Count);
        }

        [Fact]
        public async Task Block_HidesActivityFromFeed()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "ali", "veli");
            var activity = new ActivityService(context);
            var follows = new FollowService(context, activity, new NotificationService(context));

            await follows.FollowAsync("ali", "veli");
            await activity.RecordRatedAsync("veli", show.Id, RatingTargetType.Show, null, null, 4.0m);
            Assert.NotEmpty(await activity.GetFeedAsync("ali", 0, 20));

            await new BlockService(context).BlockAsync("ali", "veli");

            Assert.Empty(await activity.GetFeedAsync("ali", 0, 20));
        }

        [Fact]
        public async Task Block_HidesListsFromDiscoverAndProfile()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "sahip", "okur");
            var notifications = new NotificationService(context);
            var lists = new UserListService(context, new LocalOnlyCatalog(context), notifications);

            var list = await lists.CreateAsync("sahip", new UpsertListRequest { Title = "En iyiler", IsPublic = true });
            await lists.AddItemAsync("sahip", list!.Id, new AddListItemRequest { TmdbShowId = show.TmdbId });

            Assert.Single(await lists.GetDiscoverAsync(ListSort.Recent, 0, 20, "okur"));

            await new BlockService(context).BlockAsync("okur", "sahip");

            Assert.Empty(await lists.GetDiscoverAsync(ListSort.Recent, 0, 20, "okur"));
            Assert.Null(await lists.GetForUserAsync("sahip", "okur"));
            Assert.Null(await lists.GetDetailAsync(list.Id, "okur"));
            Assert.Null(await lists.LikeAsync("okur", list.Id));
        }

        [Fact]
        public async Task Block_SuppressesNotifications()
        {
            using var context = CreateContext();
            await SeedAsync(context, "ali", "veli");
            var notifications = new NotificationService(context);
            await new BlockService(context).BlockAsync("ali", "veli");

            await notifications.CreateAsync("ali", "veli", NotificationType.Followed);
            await notifications.CreateAsync("veli", "ali", NotificationType.Followed);

            Assert.Empty(context.Notifications);
        }

        [Fact]
        public async Task GetBlockedAsync_ListsOnlyOwnBlocks()
        {
            using var context = CreateContext();
            await SeedAsync(context, "ali", "veli", "ayse");
            var blocks = new BlockService(context);

            await blocks.BlockAsync("ali", "veli");
            await blocks.BlockAsync("ayse", "ali");

            var mine = await blocks.GetBlockedAsync("ali");

            Assert.Equal("veli", Assert.Single(mine).Username);
        }

        // ----- İçerik bildirimi -------------------------------------------------

        private static ReportService CreateReports(BingeOnDbContext context) =>
            new(context, new ActivityService(context), new NotificationService(context));

        [Fact]
        public async Task CreateAsync_RecordsContentOwner()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "yazar", "okur");
            var review = await SeedReviewAsync(context, show, "yazar");
            var reports = CreateReports(context);

            var result = await reports.CreateAsync("okur", new CreateReportRequest
            {
                TargetType = ReportTargetType.Review,
                TargetId = review.Id,
                Reason = ReportReason.UnmarkedSpoiler
            });

            Assert.Equal(ReportResult.Ok, result);
            var saved = await context.Reports.SingleAsync();
            Assert.Equal("yazar", saved.TargetUserId);
            Assert.Equal(ReportStatus.Open, saved.Status);
        }

        [Fact]
        public async Task CreateAsync_RejectsSelfDuplicateAndMissingTarget()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "yazar", "okur");
            var review = await SeedReviewAsync(context, show, "yazar");
            var reports = CreateReports(context);

            var request = new CreateReportRequest
            {
                TargetType = ReportTargetType.Review,
                TargetId = review.Id,
                Reason = ReportReason.Spam
            };

            Assert.Equal(ReportResult.Self, await reports.CreateAsync("yazar", request));
            Assert.Equal(ReportResult.Ok, await reports.CreateAsync("okur", request));
            Assert.Equal(ReportResult.AlreadyReported, await reports.CreateAsync("okur", request));
            Assert.Equal(ReportResult.TargetNotFound, await reports.CreateAsync("okur", new CreateReportRequest
            {
                TargetType = ReportTargetType.Review,
                TargetId = 9999
            }));

            Assert.Equal(1, await context.Reports.CountAsync());
        }

        [Fact]
        public async Task CreateAsync_UserReportResolvesByUsername()
        {
            using var context = CreateContext();
            await SeedAsync(context, "kaba", "okur");
            var reports = CreateReports(context);

            var result = await reports.CreateAsync("okur", new CreateReportRequest
            {
                TargetType = ReportTargetType.User,
                TargetUsername = "kaba",
                Reason = ReportReason.Harassment
            });

            Assert.Equal(ReportResult.Ok, result);
            var saved = await context.Reports.SingleAsync();
            Assert.Equal("kaba", saved.TargetUserId);
            Assert.Null(saved.TargetId);
        }

        [Fact]
        public async Task ResolveAsync_DeleteContentRemovesReviewAndClosesSiblings()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "yazar", "okur", "baskasi", "mod");
            var review = await SeedReviewAsync(context, show, "yazar");
            var reports = CreateReports(context);

            var request = new CreateReportRequest { TargetType = ReportTargetType.Review, TargetId = review.Id };
            await reports.CreateAsync("okur", request);
            await reports.CreateAsync("baskasi", request);

            var first = await context.Reports.OrderBy(r => r.Id).FirstAsync();
            var resolved = await reports.ResolveAsync("mod", first.Id,
                new ResolveReportRequest { Action = ReportAction.DeleteContent, Note = "Spoiler doluydu" });

            Assert.True(resolved);
            Assert.Empty(context.Reviews);
            // İkinci bildirim de aynı kararla kapanır; kuyrukta silinmiş içerik kalmaz.
            Assert.Equal(0, await reports.GetOpenCountAsync());
            Assert.All(await context.Reports.ToListAsync(), r => Assert.Equal(ReportStatus.Resolved, r.Status));
        }

        [Fact]
        public async Task ResolveAsync_DismissKeepsContent()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "yazar", "okur", "mod");
            var review = await SeedReviewAsync(context, show, "yazar");
            var reports = CreateReports(context);

            await reports.CreateAsync("okur",
                new CreateReportRequest { TargetType = ReportTargetType.Review, TargetId = review.Id });
            var saved = await context.Reports.SingleAsync();

            Assert.True(await reports.ResolveAsync("mod", saved.Id,
                new ResolveReportRequest { Action = ReportAction.Dismiss }));

            Assert.Single(context.Reviews);
            Assert.Equal(ReportStatus.Dismissed, (await context.Reports.SingleAsync()).Status);
            // Kapanmış bildirim ikinci kez kapatılamaz.
            Assert.False(await reports.ResolveAsync("mod", saved.Id, new ResolveReportRequest()));
        }

        [Fact]
        public async Task GetQueueAsync_ShowsExcerptAndRepeatCount()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "yazar", "okur", "baskasi");
            var review = await SeedReviewAsync(context, show, "yazar");
            var comment = new ReviewComment { ReviewId = review.Id, UserId = "yazar", Body = "Kaba yorum" };
            context.ReviewComments.Add(comment);
            await context.SaveChangesAsync();
            var reports = CreateReports(context);

            await reports.CreateAsync("okur",
                new CreateReportRequest { TargetType = ReportTargetType.Review, TargetId = review.Id });
            await reports.CreateAsync("baskasi",
                new CreateReportRequest { TargetType = ReportTargetType.ReviewComment, TargetId = comment.Id });

            var queue = await reports.GetQueueAsync(null, 0, 20);

            Assert.Equal(2, queue.Count);
            Assert.All(queue, r => Assert.Equal("yazar", r.TargetUsername));
            // İki bildirim de aynı kullanıcıya ait; her kart diğerini sayar.
            Assert.All(queue, r => Assert.Equal(1, r.OtherOpenReportsForTarget));
            Assert.Contains(queue, r => r.ContentExcerpt == "İyiydi");
            Assert.Contains(queue, r => r.ContentExcerpt == "Kaba yorum");
        }

        /// <summary>Katalogu yalnızca yerel DB'den okuyan sahte; testlerde TMDb'ye çıkılmaz.</summary>
        private sealed class LocalOnlyCatalog : IShowCatalogService
        {
            private readonly BingeOnDbContext _context;
            public LocalOnlyCatalog(BingeOnDbContext context) => _context = context;

            public Task<Show?> GetOrSyncShowAsync(int tmdbId, bool forceSync = false) =>
                _context.Shows.Include(s => s.Seasons).FirstOrDefaultAsync(s => s.TmdbId == tmdbId);

            public Task<int> SyncStaleOngoingShowsAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(0);
        }
    }
}
