using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BingeWatch.Tests
{
    public class UserListServiceTests
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

        private static UserListService CreateService(BingeOnDbContext context) =>
            new(context, new LocalOnlyCatalogService(context), new NotificationService(context));

        private static async Task SeedAsync(BingeOnDbContext context, bool isPrivateProfile = false)
        {
            context.Users.Add(new AppUser
            {
                Id = "ali",
                UserName = "ali",
                NormalizedUserName = "ALI",
                DisplayName = "Ali",
                IsPrivate = isPrivateProfile
            });
            context.Users.Add(new AppUser
            {
                Id = "veli",
                UserName = "veli",
                NormalizedUserName = "VELI",
                DisplayName = "Veli"
            });

            for (var tmdbId = 1; tmdbId <= 3; tmdbId++)
            {
                context.Shows.Add(new Show
                {
                    TmdbId = tmdbId,
                    Name = $"Dizi {tmdbId}",
                    PosterPath = $"/poster{tmdbId}.jpg",
                    LastSyncedAt = DateTime.UtcNow
                });
            }

            await context.SaveChangesAsync();
        }

        private static async Task<UserListDetailDto> CreateListWithShowsAsync(UserListService service,
            params int[] tmdbIds)
        {
            var list = await service.CreateAsync("ali", new UpsertListRequest { Title = "Polisiyeler" });
            foreach (var tmdbId in tmdbIds)
                await service.AddItemAsync("ali", list!.Id, new AddListItemRequest { TmdbShowId = tmdbId });

            return (await service.GetDetailAsync(list!.Id, "ali"))!;
        }

        [Fact]
        public async Task CreateAsync_RejectsEmptyTitle()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var result = await service.CreateAsync("ali", new UpsertListRequest { Title = "   " });

            Assert.Null(result);
            Assert.Empty(context.UserLists);
        }

        [Fact]
        public async Task AddItemAsync_AppendsToEndInOrder()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var list = await CreateListWithShowsAsync(service, 1, 2, 3);

            Assert.Equal(new[] { 1, 2, 3 }, list.Items.Select(i => i.TmdbShowId));
            Assert.Equal(new[] { 0, 1, 2 }, list.Items.Select(i => i.Position));
            Assert.Equal(3, list.ItemCount);
        }

        [Fact]
        public async Task AddItemAsync_SameShowTwiceDoesNotDuplicate()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var created = await service.CreateAsync("ali", new UpsertListRequest { Title = "Liste" });
            await service.AddItemAsync("ali", created!.Id, new AddListItemRequest { TmdbShowId = 1 });
            var second = await service.AddItemAsync("ali", created.Id,
                new AddListItemRequest { TmdbShowId = 1, Note = "yine de iyi" });

            Assert.Equal(1, await context.UserListItems.CountAsync());
            Assert.Equal("yine de iyi", second!.Note);
        }

        [Fact]
        public async Task AddItemAsync_RejectsNonOwner()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var created = await service.CreateAsync("ali", new UpsertListRequest { Title = "Liste" });
            var result = await service.AddItemAsync("veli", created!.Id, new AddListItemRequest { TmdbShowId = 1 });

            Assert.Null(result);
            Assert.Empty(context.UserListItems);
        }

        [Fact]
        public async Task RemoveItemAsync_CompactsPositions()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var list = await CreateListWithShowsAsync(service, 1, 2, 3);
            var middle = list.Items.Single(i => i.TmdbShowId == 2);

            var removed = await service.RemoveItemAsync("ali", list.Id, middle.Id);
            var reloaded = await service.GetDetailAsync(list.Id, "ali");

            Assert.True(removed);
            Assert.Equal(new[] { 1, 3 }, reloaded!.Items.Select(i => i.TmdbShowId));
            Assert.Equal(new[] { 0, 1 }, reloaded.Items.Select(i => i.Position));
        }

        [Fact]
        public async Task ReorderAsync_AppliesRequestedOrder()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var list = await CreateListWithShowsAsync(service, 1, 2, 3);
            var reversed = list.Items.Select(i => i.Id).Reverse().ToList();

            var result = await service.ReorderAsync("ali", list.Id,
                new ReorderListRequest { ItemIds = reversed });

            Assert.Equal(new[] { 3, 2, 1 }, result!.Items.Select(i => i.TmdbShowId));
            Assert.Equal(new[] { 0, 1, 2 }, result.Items.Select(i => i.Position));
        }

        [Fact]
        public async Task ReorderAsync_KeepsOmittedItemsAtEnd()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var list = await CreateListWithShowsAsync(service, 1, 2, 3);
            var third = list.Items.Single(i => i.TmdbShowId == 3);

            // Yalnızca son öğe gönderiliyor; kalanlar eski sıralarıyla arkaya alınmalı.
            var result = await service.ReorderAsync("ali", list.Id,
                new ReorderListRequest { ItemIds = new List<int> { third.Id } });

            Assert.Equal(new[] { 3, 1, 2 }, result!.Items.Select(i => i.TmdbShowId));
        }

        [Fact]
        public async Task ReorderAsync_RejectsNonOwner()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var list = await CreateListWithShowsAsync(service, 1, 2);
            var result = await service.ReorderAsync("veli", list.Id,
                new ReorderListRequest { ItemIds = list.Items.Select(i => i.Id).Reverse().ToList() });

            Assert.Null(result);
        }

        [Fact]
        public async Task GetDetailAsync_PrivateListHiddenFromOthers()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var created = await service.CreateAsync("ali",
                new UpsertListRequest { Title = "Gizli", IsPublic = false });

            Assert.NotNull(await service.GetDetailAsync(created!.Id, "ali"));
            Assert.Null(await service.GetDetailAsync(created.Id, "veli"));
            Assert.Null(await service.GetDetailAsync(created.Id, null));
        }

        [Fact]
        public async Task GetDetailAsync_ListsOfPrivateProfileHiddenFromOthers()
        {
            using var context = CreateContext();
            await SeedAsync(context, isPrivateProfile: true);
            var service = CreateService(context);

            var created = await service.CreateAsync("ali",
                new UpsertListRequest { Title = "Açık ama profil gizli", IsPublic = true });

            Assert.NotNull(await service.GetDetailAsync(created!.Id, "ali"));
            Assert.Null(await service.GetDetailAsync(created.Id, "veli"));
        }

        [Fact]
        public async Task GetForUserAsync_HidesPrivateListsFromOthers()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            await service.CreateAsync("ali", new UpsertListRequest { Title = "Açık", IsPublic = true });
            await service.CreateAsync("ali", new UpsertListRequest { Title = "Kapalı", IsPublic = false });

            var own = await service.GetForUserAsync("ali", "ali");
            var others = await service.GetForUserAsync("ali", "veli");

            Assert.Equal(2, own!.Count);
            Assert.True(own.All(l => l.IsOwner));
            Assert.Single(others!);
            Assert.Equal("Açık", others![0].Title);
            Assert.False(others[0].IsOwner);
        }

        [Fact]
        public async Task GetForUserAsync_PrivateProfileReturnsNullForOthers()
        {
            using var context = CreateContext();
            await SeedAsync(context, isPrivateProfile: true);
            var service = CreateService(context);

            await service.CreateAsync("ali", new UpsertListRequest { Title = "Açık" });

            Assert.NotNull(await service.GetForUserAsync("ali", "ali"));
            Assert.Null(await service.GetForUserAsync("ali", "veli"));
        }

        [Fact]
        public async Task GetForUserAsync_SummaryCarriesCountAndPreviewPosters()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            await CreateListWithShowsAsync(service, 1, 2, 3);

            var lists = await service.GetForUserAsync("ali", "ali");

            Assert.Single(lists!);
            Assert.Equal(3, lists![0].ItemCount);
            Assert.Equal(new[] { "/poster1.jpg", "/poster2.jpg", "/poster3.jpg" },
                lists[0].PreviewPosterPaths);
        }

        [Fact]
        public async Task DeleteAsync_RemovesListAndItems()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var list = await CreateListWithShowsAsync(service, 1, 2);

            Assert.False(await service.DeleteAsync("veli", list.Id));
            Assert.True(await service.DeleteAsync("ali", list.Id));
            Assert.Empty(context.UserLists);
        }

        [Fact]
        public async Task LikeAsync_IsIdempotentAndNotifiesOwner()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var list = await CreateListWithShowsAsync(service, 1);

            var first = await service.LikeAsync("veli", list.Id);
            var second = await service.LikeAsync("veli", list.Id);

            Assert.Equal(1, first!.LikeCount);
            Assert.True(first.LikedByViewer);
            Assert.Equal(1, second!.LikeCount);
            Assert.Equal(1, await context.UserListLikes.CountAsync());

            var notification = await context.Notifications.SingleAsync();
            Assert.Equal("ali", notification.UserId);
            Assert.Equal("veli", notification.ActorId);
            Assert.Equal(NotificationType.ListLiked, notification.Type);
            Assert.Equal(list.Id, notification.UserListId);
        }

        [Fact]
        public async Task UnlikeAsync_RemovesLikeAndNotification()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var list = await CreateListWithShowsAsync(service, 1);
            await service.LikeAsync("veli", list.Id);

            var state = await service.UnlikeAsync("veli", list.Id);

            Assert.Equal(0, state!.LikeCount);
            Assert.False(state.LikedByViewer);
            Assert.Empty(context.UserListLikes);
            Assert.Empty(context.Notifications);
        }

        [Fact]
        public async Task LikeAsync_OwnLikeDoesNotNotify()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var list = await CreateListWithShowsAsync(service, 1);
            await service.LikeAsync("ali", list.Id);

            Assert.Equal(1, await context.UserListLikes.CountAsync());
            Assert.Empty(context.Notifications);
        }

        [Fact]
        public async Task LikeAsync_PrivateListCannotBeLikedByOthers()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var created = await service.CreateAsync("ali",
                new UpsertListRequest { Title = "Gizli", IsPublic = false });

            Assert.Null(await service.LikeAsync("veli", created!.Id));
            Assert.Empty(context.UserListLikes);
        }

        [Fact]
        public async Task DeleteAsync_RemovesLikeNotifications()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var list = await CreateListWithShowsAsync(service, 1);
            await service.LikeAsync("veli", list.Id);

            await service.DeleteAsync("ali", list.Id);

            Assert.Empty(context.Notifications);
        }

        [Fact]
        public async Task GetDiscoverAsync_OnlyPublicNonEmptyListsOfVisibleProfiles()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            await CreateListWithShowsAsync(service, 1);
            await service.CreateAsync("ali", new UpsertListRequest { Title = "Boş liste" });
            var hidden = await service.CreateAsync("ali",
                new UpsertListRequest { Title = "Kapalı", IsPublic = false });
            await service.AddItemAsync("ali", hidden!.Id, new AddListItemRequest { TmdbShowId = 2 });

            var discover = (await service.GetDiscoverAsync(ListSort.Recent, null, 20, "veli")).Items;

            Assert.Single(discover);
            Assert.Equal("Polisiyeler", discover[0].Title);
            Assert.Equal("Ali", discover[0].OwnerDisplayName);
            Assert.False(discover[0].IsOwner);
        }

        [Fact]
        public async Task GetDiscoverAsync_HidesListsOfPrivateProfiles()
        {
            using var context = CreateContext();
            await SeedAsync(context, isPrivateProfile: true);
            var service = CreateService(context);

            await CreateListWithShowsAsync(service, 1);

            Assert.Empty((await service.GetDiscoverAsync(ListSort.Recent, null, 20, "veli")).Items);
        }

        [Fact]
        public async Task GetDiscoverAsync_MostLikedOrdersByLikeCount()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var quiet = await CreateListWithShowsAsync(service, 1);
            var popular = await service.CreateAsync("ali", new UpsertListRequest { Title = "Popüler" });
            await service.AddItemAsync("ali", popular!.Id, new AddListItemRequest { TmdbShowId = 2 });
            await service.LikeAsync("veli", popular.Id);

            var discover = (await service.GetDiscoverAsync(ListSort.MostLiked, null, 20, "veli")).Items;

            Assert.Equal(new[] { popular.Id, quiet.Id }, discover.Select(l => l.Id));
            Assert.Equal(1, discover[0].LikeCount);
            Assert.True(discover[0].LikedByViewer);
            Assert.False(discover[1].LikedByViewer);
        }

        [Fact]
        public async Task GetMembershipAsync_MarksListsContainingShow()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            var withShow = await service.CreateAsync("ali", new UpsertListRequest { Title = "İçinde var" });
            await service.AddItemAsync("ali", withShow!.Id, new AddListItemRequest { TmdbShowId = 1 });
            await service.CreateAsync("ali", new UpsertListRequest { Title = "Boş" });

            var membership = await service.GetMembershipAsync("ali", 1);

            Assert.Equal(2, membership.Count);
            Assert.True(membership.Single(m => m.ListId == withShow.Id).ContainsShow);
            Assert.False(membership.Single(m => m.ListId != withShow.Id).ContainsShow);
        }

        [Fact]
        public async Task GetMembershipAsync_UnknownShowMarksNothing()
        {
            using var context = CreateContext();
            await SeedAsync(context);
            var service = CreateService(context);

            await service.CreateAsync("ali", new UpsertListRequest { Title = "Liste" });

            // Katalogda olmayan dizi: üyelik sorgusu TMDb'ye çıkmadan boş döner.
            var membership = await service.GetMembershipAsync("ali", 999);

            Assert.Single(membership);
            Assert.False(membership[0].ContainsShow);
        }
    }
}
