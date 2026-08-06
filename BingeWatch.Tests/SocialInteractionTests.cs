using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BingeWatch.Tests
{
    /// <summary>İnceleme beğeni/yorumları, bildirimler, arkadaş puanları ve profil istatistikleri.</summary>
    public class SocialInteractionTests
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

            var season = new Season { ShowId = show.Id, SeasonNumber = 1, EpisodeCount = 2 };
            context.Seasons.Add(season);
            await context.SaveChangesAsync();

            context.Episodes.Add(new Episode { SeasonId = season.Id, EpisodeNumber = 1, Name = "Bölüm 1", Runtime = 45 });
            context.Episodes.Add(new Episode { SeasonId = season.Id, EpisodeNumber = 2, Name = "Bölüm 2", Runtime = 45 });
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

        // ----- Beğeni ---------------------------------------------------------

        [Fact]
        public async Task LikeAsync_IsIdempotentAndNotifiesAuthor()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "yazar", "okur");
            var review = await SeedReviewAsync(context, show, "yazar");
            var notifications = new NotificationService(context);
            var service = new ReviewInteractionService(context, notifications);

            await service.LikeAsync("okur", review.Id);
            var state = await service.LikeAsync("okur", review.Id);

            Assert.NotNull(state);
            Assert.Equal(1, state!.LikeCount);
            Assert.True(state.LikedByViewer);
            Assert.Equal(1, await context.ReviewLikes.CountAsync());
            Assert.Equal(1, await notifications.GetUnreadCountAsync("yazar"));
        }

        [Fact]
        public async Task UnlikeAsync_RemovesLikeAndNotification()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "yazar", "okur");
            var review = await SeedReviewAsync(context, show, "yazar");
            var notifications = new NotificationService(context);
            var service = new ReviewInteractionService(context, notifications);

            await service.LikeAsync("okur", review.Id);
            var state = await service.UnlikeAsync("okur", review.Id);

            Assert.Equal(0, state!.LikeCount);
            Assert.False(state.LikedByViewer);
            Assert.Empty(context.ReviewLikes);
            Assert.Equal(0, await notifications.GetUnreadCountAsync("yazar"));
        }

        [Fact]
        public async Task LikeAsync_OwnReviewDoesNotNotify()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "yazar");
            var review = await SeedReviewAsync(context, show, "yazar");
            var notifications = new NotificationService(context);
            var service = new ReviewInteractionService(context, notifications);

            await service.LikeAsync("yazar", review.Id);

            Assert.Equal(1, await context.ReviewLikes.CountAsync());
            Assert.Empty(context.Notifications);
        }

        // ----- Yorum ----------------------------------------------------------

        [Fact]
        public async Task AddCommentAsync_RejectsEmptyBody()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "yazar", "okur");
            var review = await SeedReviewAsync(context, show, "yazar");
            var service = new ReviewInteractionService(context, new NotificationService(context));

            var result = await service.AddCommentAsync("okur", review.Id, new AddCommentRequest { Body = "   " });

            Assert.Null(result);
            Assert.Empty(context.ReviewComments);
        }

        [Fact]
        public async Task AddCommentAsync_NotifiesAuthorAndMarksDeletePermissions()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "yazar", "okur", "yabanci");
            var review = await SeedReviewAsync(context, show, "yazar");
            var notifications = new NotificationService(context);
            var service = new ReviewInteractionService(context, notifications);

            var comment = await service.AddCommentAsync("okur", review.Id, new AddCommentRequest { Body = "Katılıyorum" });

            Assert.NotNull(comment);
            Assert.Equal(1, await notifications.GetUnreadCountAsync("yazar"));

            // Yorumun sahibi ve incelemenin sahibi silebilir, üçüncü kişi silemez.
            var forOwner = await service.GetCommentsAsync(review.Id, "okur");
            var forAuthor = await service.GetCommentsAsync(review.Id, "yazar");
            var forStranger = await service.GetCommentsAsync(review.Id, "yabanci");

            Assert.True(forOwner![0].CanDelete);
            Assert.True(forAuthor![0].CanDelete);
            Assert.False(forStranger![0].CanDelete);
        }

        [Fact]
        public async Task DeleteCommentAsync_OnlyOwnerOrReviewAuthor()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "yazar", "okur", "yabanci");
            var review = await SeedReviewAsync(context, show, "yazar");
            var service = new ReviewInteractionService(context, new NotificationService(context));
            var comment = await service.AddCommentAsync("okur", review.Id, new AddCommentRequest { Body = "Selam" });

            Assert.False(await service.DeleteCommentAsync("yabanci", comment!.Id));
            Assert.True(await service.DeleteCommentAsync("yazar", comment.Id));
            Assert.Empty(context.ReviewComments);
        }

        [Fact]
        public async Task ReviewService_ProjectsLikeAndCommentCounts()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "yazar", "okur");
            var review = await SeedReviewAsync(context, show, "yazar");
            var notifications = new NotificationService(context);
            var interactions = new ReviewInteractionService(context, notifications);
            var reviews = new ReviewService(context, new LocalOnlyCatalog(context),
                new ActivityService(context), notifications);

            await interactions.LikeAsync("okur", review.Id);
            await interactions.AddCommentAsync("okur", review.Id, new AddCommentRequest { Body = "Bence de" });

            var forOkur = await reviews.GetForShowAsync(show.TmdbId, null, "okur");
            var forAnon = await reviews.GetForShowAsync(show.TmdbId);

            Assert.Equal(1, forOkur[0].LikeCount);
            Assert.True(forOkur[0].LikedByViewer);
            Assert.Equal(1, forOkur[0].CommentCount);
            Assert.False(forAnon[0].LikedByViewer);
        }

        [Fact]
        public async Task ReviewService_DeletingReviewRemovesNotifications()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "yazar", "okur");
            var review = await SeedReviewAsync(context, show, "yazar");
            var notifications = new NotificationService(context);
            var interactions = new ReviewInteractionService(context, notifications);
            var reviews = new ReviewService(context, new LocalOnlyCatalog(context),
                new ActivityService(context), notifications);

            await interactions.LikeAsync("okur", review.Id);
            Assert.Equal(1, await notifications.GetUnreadCountAsync("yazar"));

            await reviews.DeleteAsync("yazar", review.Id);

            Assert.Empty(context.Notifications);
        }

        // ----- Bildirim -------------------------------------------------------

        [Fact]
        public async Task MarkAllReadAsync_ClearsUnreadCount()
        {
            using var context = CreateContext();
            await SeedAsync(context, "ali", "veli");
            var service = new NotificationService(context);

            await service.CreateAsync("ali", "veli", NotificationType.Followed);
            Assert.Equal(1, await service.GetUnreadCountAsync("ali"));

            var marked = await service.MarkAllReadAsync("ali");

            Assert.Equal(1, marked);
            Assert.Equal(0, await service.GetUnreadCountAsync("ali"));
            Assert.Single((await service.GetAsync("ali", null, 20)).Items);
        }

        [Fact]
        public async Task FollowService_WritesAndRemovesNotification()
        {
            using var context = CreateContext();
            await SeedAsync(context, "ali", "veli");
            var notifications = new NotificationService(context);
            var follows = new FollowService(context, new ActivityService(context), notifications);

            await follows.FollowAsync("ali", "veli");
            Assert.Equal(1, await notifications.GetUnreadCountAsync("veli"));

            await follows.UnfollowAsync("ali", "veli");
            Assert.Empty(context.Notifications);
        }

        [Fact]
        public async Task RemoveAsync_KeepsNotificationThatWasAlreadyRead()
        {
            using var context = CreateContext();
            await SeedAsync(context, "ali", "veli");
            var notifications = new NotificationService(context);
            var follows = new FollowService(context, new ActivityService(context), notifications);

            await follows.FollowAsync("ali", "veli");
            await notifications.MarkAllReadAsync("veli");

            await follows.UnfollowAsync("ali", "veli");

            // Okunmuş bildirim, eylem geri alınsa da geçmişte kalmalı: kullanıcı
            // onu görmüştü, listeden yok olması geçmişi değiştirmek olurdu.
            var remaining = await notifications.GetAsync("veli", null, 20);
            Assert.Single(remaining.Items);
            Assert.True(remaining.Items[0].IsRead);
            Assert.Equal(0, await notifications.GetUnreadCountAsync("veli"));
        }

        [Fact]
        public async Task RemoveAsync_RemovesOnlyTheUnreadNotification()
        {
            using var context = CreateContext();
            await SeedAsync(context, "ali", "veli", "ayse");
            var notifications = new NotificationService(context);

            // Ali'ninki okunmuş, Ayşe'ninki okunmamış olacak.
            await notifications.CreateAsync("veli", "ali", NotificationType.Followed);
            await notifications.MarkAllReadAsync("veli");
            await notifications.CreateAsync("veli", "ayse", NotificationType.Followed);

            await notifications.RemoveAsync("veli", "ali", NotificationType.Followed);
            await notifications.RemoveAsync("veli", "ayse", NotificationType.Followed);

            var remaining = await notifications.GetAsync("veli", null, 20);
            Assert.Single(remaining.Items);
            Assert.Equal("ali", remaining.Items[0].ActorUsername);
        }

        // ----- Arkadaş puanları ----------------------------------------------

        [Fact]
        public async Task GetFriendRatingsAsync_OnlyCountsFolloweesShowLevelRatings()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "ali", "veli", "yabanci");
            var activity = new ActivityService(context);
            var ratings = new RatingService(context, activity);

            context.Follows.Add(new Follow { FollowerId = "ali", FolloweeId = "veli" });
            await context.SaveChangesAsync();

            await ratings.SetRatingAsync("veli", show.TmdbId,
                new SetRatingRequest { TargetType = RatingTargetType.Show, Value = 4.0m });
            await ratings.SetRatingAsync("veli", show.TmdbId,
                new SetRatingRequest { TargetType = RatingTargetType.Season, SeasonNumber = 1, Value = 2.0m });
            await ratings.SetRatingAsync("yabanci", show.TmdbId,
                new SetRatingRequest { TargetType = RatingTargetType.Show, Value = 1.0m });

            var friends = await ratings.GetFriendRatingsAsync("ali", show.TmdbId);

            // Takip edilmeyenin puanı ve sezon puanı karta girmez.
            Assert.Equal(1, friends!.Count);
            Assert.Equal(4.0, friends.Average);
            Assert.Equal("veli", friends.Ratings.Single().Username);
        }

        [Fact]
        public async Task GetFriendRatingsAsync_EmptyWhenFollowingNobody()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "ali");
            var ratings = new RatingService(context, new ActivityService(context));

            var friends = await ratings.GetFriendRatingsAsync("ali", show.TmdbId);

            Assert.Equal(0, friends!.Count);
            Assert.Null(friends.Average);
        }

        // ----- Profil istatistikleri ------------------------------------------

        [Fact]
        public async Task GetStatsAsync_AggregatesWatchTimeAndYears()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "ali");
            var episodes = await context.Episodes.OrderBy(e => e.EpisodeNumber).ToListAsync();

            context.WatchedEpisodes.Add(new WatchedEpisode
            {
                UserId = "ali",
                EpisodeId = episodes[0].Id,
                WatchedAt = new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc)
            });
            context.WatchedEpisodes.Add(new WatchedEpisode
            {
                UserId = "ali",
                EpisodeId = episodes[1].Id,
                WatchedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)
            });
            context.UserShows.Add(new UserShow
            {
                UserId = "ali",
                ShowId = show.Id,
                Status = WatchStatus.Watching,
                IsFavorite = true
            });
            await context.SaveChangesAsync();

            var stats = await new UserStatsService(context).GetStatsAsync("ali", "ali");

            Assert.NotNull(stats);
            Assert.Equal(2, stats!.WatchedEpisodeCount);
            Assert.Equal(90, stats.TotalMinutes);
            Assert.Equal(1, stats.ShowsWatchingCount);
            Assert.Equal(new[] { 2025, 2026 }, stats.YearlyCounts.Select(y => y.Year));
            Assert.Equal("Test Show", stats.FavoriteShows.Single().Name);
        }

        [Fact]
        public async Task GetStatsAsync_PrivateProfileVisibleOnlyToOwner()
        {
            using var context = CreateContext();
            await SeedAsync(context, "gizli", "ali");
            var user = await context.Users.FirstAsync(u => u.Id == "gizli");
            user.IsPrivate = true;
            await context.SaveChangesAsync();
            var service = new UserStatsService(context);

            Assert.Null(await service.GetStatsAsync("gizli", "ali"));
            Assert.NotNull(await service.GetStatsAsync("gizli", "gizli"));
        }

        [Fact]
        public async Task SetFavoriteAsync_RequiresShowInUserList()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "ali");
            var service = new UserStatsService(context);

            Assert.False(await service.SetFavoriteAsync("ali", show.TmdbId, true));

            context.UserShows.Add(new UserShow { UserId = "ali", ShowId = show.Id });
            await context.SaveChangesAsync();

            Assert.True(await service.SetFavoriteAsync("ali", show.TmdbId, true));
            Assert.True(await service.IsFavoriteAsync("ali", show.TmdbId));
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
