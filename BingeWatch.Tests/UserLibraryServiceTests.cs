using BingeWatch.API.Data;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BingeWatch.Tests
{
    /// <summary>
    /// Kütüphane yeni bir okuma yüzeyi; gizlilik ve engelleme süzgeçleri burada
    /// da tutmalı. Unutulursa hiçbir şey kırılmaz, yalnızca sessizce sızar —
    /// testlerin asıl işi bu.
    /// </summary>
    public class UserLibraryServiceTests
    {
        private static BingeOnDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<BingeOnDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new BingeOnDbContext(options);
        }

        /// <summary>
        /// "ali"nin kütüphanesi: biri izleniyor, biri bitmiş, biri izlenecek.
        /// Eklenme tarihleri bilerek farklı — sıralama sınanabilsin diye.
        /// </summary>
        private static async Task SeedAsync(BingeOnDbContext context, bool isPrivate = false)
        {
            context.Users.AddRange(
                new AppUser
                {
                    Id = "ali",
                    UserName = "ali",
                    NormalizedUserName = "ALI",
                    DisplayName = "Ali",
                    IsPrivate = isPrivate
                },
                new AppUser
                {
                    Id = "veli",
                    UserName = "veli",
                    NormalizedUserName = "VELI",
                    DisplayName = "Veli"
                });

            var watching = new Show { TmdbId = 1, Name = "İzlenen", LastSyncedAt = DateTime.UtcNow };
            var finished = new Show { TmdbId = 2, Name = "Biten", LastSyncedAt = DateTime.UtcNow };
            var planned = new Show { TmdbId = 3, Name = "Planlanan", LastSyncedAt = DateTime.UtcNow };
            context.Shows.AddRange(watching, finished, planned);
            await context.SaveChangesAsync();

            context.UserShows.AddRange(
                new UserShow
                {
                    UserId = "ali",
                    ShowId = watching.Id,
                    Status = WatchStatus.Watching,
                    IsFavorite = true,
                    AddedAt = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc)
                },
                new UserShow
                {
                    UserId = "ali",
                    ShowId = finished.Id,
                    Status = WatchStatus.Completed,
                    AddedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
                },
                new UserShow
                {
                    UserId = "ali",
                    ShowId = planned.Id,
                    Status = WatchStatus.PlanToWatch,
                    AddedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                });

            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task GetLibraryAsync_ReturnsEveryStatusWithNewestFirst()
        {
            using var context = CreateContext();
            await SeedAsync(context);

            var library = await new UserLibraryService(context).GetLibraryAsync("ali", viewerId: "veli");

            Assert.NotNull(library);
            Assert.Equal("ali", library!.Username);
            Assert.Equal(new[] { "İzlenen", "Biten", "Planlanan" }, library.Shows.Select(s => s.Name));
            Assert.Equal(new[] { "Watching", "Completed", "PlanToWatch" }, library.Shows.Select(s => s.Status));

            // "İzleyecekleri" sekmesi bu satırdan çiziliyor.
            Assert.Single(library.Shows, s => s.Status == nameof(WatchStatus.PlanToWatch));
            Assert.True(library.Shows[0].IsFavorite);
        }

        [Fact]
        public async Task GetLibraryAsync_HidesPrivateProfileFromOthers()
        {
            using var context = CreateContext();
            await SeedAsync(context, isPrivate: true);
            var service = new UserLibraryService(context);

            Assert.Null(await service.GetLibraryAsync("ali", viewerId: "veli"));
            Assert.Null(await service.GetLibraryAsync("ali", viewerId: null));

            // Sahibi kendi kütüphanesini görmeye devam ediyor.
            Assert.NotNull(await service.GetLibraryAsync("ali", viewerId: "ali"));
        }

        [Theory]
        [InlineData("ali", "veli")]
        [InlineData("veli", "ali")]
        public async Task GetLibraryAsync_HidesLibraryFromBlockedParties(string blocker, string blocked)
        {
            using var context = CreateContext();
            await SeedAsync(context);

            context.UserBlocks.Add(new UserBlock { BlockerId = blocker, BlockedId = blocked });
            await context.SaveChangesAsync();

            // Engel tek yönlü kaydediliyor ama iki yönlü etki ediyor; hangi
            // yönde engellendiği de sızmamalı.
            Assert.Null(await new UserLibraryService(context).GetLibraryAsync("ali", viewerId: "veli"));
        }

        [Fact]
        public async Task GetLibraryAsync_ReturnsNullForUnknownUser()
        {
            using var context = CreateContext();
            await SeedAsync(context);

            Assert.Null(await new UserLibraryService(context).GetLibraryAsync("yok", viewerId: "veli"));
        }

        [Fact]
        public async Task GetLibraryAsync_ReturnsEmptyLibraryRatherThanNull()
        {
            using var context = CreateContext();
            await SeedAsync(context);

            // Hiç dizisi olmayan kullanıcı "bulunamadı" değil, boş kütüphane.
            var library = await new UserLibraryService(context).GetLibraryAsync("veli", viewerId: "ali");

            Assert.NotNull(library);
            Assert.Empty(library!.Shows);
        }
    }
}
