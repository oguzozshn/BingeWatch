using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BingeWatch.Tests
{
    /// <summary>İmleç kodlaması ve imleçle sayfalama (Faz 6.2).</summary>
    public class PaginationTests
    {
        private static BingeOnDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<BingeOnDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new BingeOnDbContext(options);
        }

        // ----- İmleç kodlaması --------------------------------------------------

        [Fact]
        public void Keyset_RoundTrips()
        {
            var timestamp = new DateTime(2026, 7, 27, 12, 34, 56, DateTimeKind.Utc);

            var decoded = Cursor.DecodeKeyset(Cursor.EncodeKeyset(timestamp, 42));

            Assert.NotNull(decoded);
            Assert.Equal(timestamp, decoded!.Value.Timestamp);
            Assert.Equal(42, decoded.Value.Id);
        }

        [Fact]
        public void Offset_RoundTrips()
        {
            Assert.Equal(60, Cursor.DecodeOffset(Cursor.EncodeOffset(60)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("bu-base64-degil")]
        [InlineData("Zm9vOmJhcjpiYXo=")] // geçerli base64, yanlış biçim
        public void MalformedCursor_FallsBackToStart(string? cursor)
        {
            // Bozuk imleç 400 değil "listenin başı" demek: eski biçimli bir imleci
            // geri gönderen istemci hata almak yerine baştan okumaya devam etsin.
            Assert.Null(Cursor.DecodeKeyset(cursor));
            Assert.Equal(0, Cursor.DecodeOffset(cursor));
        }

        [Fact]
        public void CursorKinds_AreNotInterchangeable()
        {
            // Offset imleci keyset olarak çözülmemeli, tersi de öyle.
            Assert.Null(Cursor.DecodeKeyset(Cursor.EncodeOffset(20)));
            Assert.Equal(0, Cursor.DecodeOffset(Cursor.EncodeKeyset(DateTime.UtcNow, 1)));
        }

        [Fact]
        public void NegativeOffset_IsClamped()
        {
            Assert.Equal(0, Cursor.DecodeOffset(Cursor.EncodeOffset(-5)));
        }

        // ----- Akışta imleçle sayfalama ----------------------------------------

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

        [Fact]
        public async Task Feed_PagesWithoutSkippingOrRepeating()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "ali");
            var service = new ActivityService(context);

            // Beş olay, hepsi ayrı saniyede — sıra deterministik olsun.
            var baseTime = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            for (var i = 0; i < 5; i++)
            {
                context.ActivityEvents.Add(new ActivityEvent
                {
                    UserId = "ali",
                    Type = ActivityType.Rated,
                    ShowId = show.Id,
                    RatingValue = 4.0m,
                    SeasonNumber = i,
                    CreatedAt = baseTime.AddMinutes(i)
                });
            }
            await context.SaveChangesAsync();

            var seen = new List<int>();
            string? cursor = null;
            var pages = 0;

            do
            {
                var page = await service.GetFeedAsync("ali", cursor, take: 2);
                seen.AddRange(page.Items.Select(i => i.Id));
                cursor = page.NextCursor;
                pages++;
            } while (cursor != null && pages < 10);

            Assert.Equal(5, seen.Count);
            Assert.Equal(seen.Count, seen.Distinct().Count());
            // En yeniden eskiye: id'ler azalan sırada (hepsi aynı kullanıcının).
            Assert.Equal(seen.OrderByDescending(id => id), seen);
        }

        [Fact]
        public async Task Feed_NewEventDoesNotShiftNextPage()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "ali");
            var service = new ActivityService(context);

            var baseTime = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            for (var i = 0; i < 4; i++)
            {
                context.ActivityEvents.Add(new ActivityEvent
                {
                    UserId = "ali",
                    Type = ActivityType.Rated,
                    ShowId = show.Id,
                    SeasonNumber = i,
                    CreatedAt = baseTime.AddMinutes(i)
                });
            }
            await context.SaveChangesAsync();

            var first = await service.GetFeedAsync("ali", cursor: null, take: 2);

            // İlk sayfadan sonra listenin başına yeni olay giriyor. Offset ile
            // ikinci sayfa bir satır kayar ve zaten görülen satır tekrar gelirdi.
            context.ActivityEvents.Add(new ActivityEvent
            {
                UserId = "ali",
                Type = ActivityType.Rated,
                ShowId = show.Id,
                SeasonNumber = 99,
                CreatedAt = baseTime.AddMinutes(10)
            });
            await context.SaveChangesAsync();

            var second = await service.GetFeedAsync("ali", first.NextCursor, take: 2);

            Assert.Empty(first.Items.Select(i => i.Id).Intersect(second.Items.Select(i => i.Id)));
        }

        [Fact]
        public async Task Feed_LastPageReturnsNullCursor()
        {
            using var context = CreateContext();
            var show = await SeedAsync(context, "ali");
            var service = new ActivityService(context);

            context.ActivityEvents.Add(new ActivityEvent
            {
                UserId = "ali",
                Type = ActivityType.Rated,
                ShowId = show.Id
            });
            await context.SaveChangesAsync();

            var page = await service.GetFeedAsync("ali", cursor: null, take: 20);

            Assert.Single(page.Items);
            Assert.Null(page.NextCursor);

            // Hiç olayı olmayan kullanıcıda da imleç yok.
            var empty = await service.GetFeedAsync("bos", cursor: null, take: 20);
            Assert.Empty(empty.Items);
            Assert.Null(empty.NextCursor);
        }
    }
}
