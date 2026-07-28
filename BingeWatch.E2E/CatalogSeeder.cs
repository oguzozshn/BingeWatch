using BingeWatch.API.Data;
using BingeWatch.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BingeWatch.E2E
{
    /// <summary>
    /// E2E veritabanına sabit bir katalog yazar.
    /// <para>
    /// Testler TMDb'ye bağlı olmamalı: anahtar geliştiricinin kişisel hesabından
    /// geliyor, istek kotası var ve dışarıdaki veri habersiz değişiyor — "Breaking
    /// Bad kaç sezon" sorusunun cevabı testin kontrolünde olmalı. Katalog satırı
    /// <see cref="Show.TmdbStatus"/> = "Ended" ve taze <see cref="Show.LastSyncedAt"/>
    /// ile yazılıyor; <c>ShowCatalogService</c> bu satırı bayat saymadığı için TMDb'ye
    /// hiç gitmiyor.
    /// </para>
    /// </summary>
    public static class CatalogSeeder
    {
        /// <summary>Tohumlanan dizinin TMDb kimliği (Breaking Bad).</summary>
        public const int ShowTmdbId = 1396;

        public const string ShowName = "Breaking Bad";

        /// <summary>Sezon numarası → bölüm sayısı. Özel bölümler (sezon 0) bilinçli olarak yok.</summary>
        private static readonly (int Season, int Episodes)[] Layout =
        {
            (1, 7), (2, 13), (3, 13), (4, 13), (5, 16)
        };

        public static int SeasonCount => Layout.Length;

        public static int EpisodeCount => Layout.Sum(l => l.Episodes);

        public static async Task SeedAsync(string connectionString)
        {
            var options = new DbContextOptionsBuilder<BingeOnDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            await using var context = new BingeOnDbContext(options);

            var show = await context.Shows
                .Include(s => s.Seasons)
                .ThenInclude(s => s.Episodes)
                .FirstOrDefaultAsync(s => s.TmdbId == ShowTmdbId);

            if (show == null)
            {
                show = new Show { TmdbId = ShowTmdbId };
                context.Shows.Add(show);
            }

            show.Name = ShowName;
            show.Overview = "Kanser teşhisi konan bir kimya öğretmeni metamfetamin üretmeye başlar.";
            // TMDb sözleşmesi: göreli yol, tam URL değil.
            show.PosterPath = "/ggFHVNu6YYI5L9pCfOacjizRGt.jpg";
            show.BackdropPath = "/tsRy63Mu5cu8etL1X7ZLyf7UP1M.jpg";
            show.FirstAirDate = new DateTime(2008, 1, 20);
            show.ImdbId = "tt0903747";
            // "Ended" + taze damga = katalog bayat değil = TMDb çağrısı yok.
            show.TmdbStatus = "Ended";
            show.VoteAverage = 8.9;
            show.VoteCount = 12000;
            show.LastSyncedAt = DateTime.UtcNow;

            foreach (var (seasonNumber, episodeCount) in Layout)
            {
                var season = show.Seasons.FirstOrDefault(s => s.SeasonNumber == seasonNumber);
                if (season == null)
                {
                    season = new Season { SeasonNumber = seasonNumber };
                    show.Seasons.Add(season);
                }

                season.Name = $"{seasonNumber}. Sezon";
                season.AirDate = new DateTime(2007 + seasonNumber, 1, 20);
                season.EpisodeCount = episodeCount;

                for (var number = 1; number <= episodeCount; number++)
                {
                    var episode = season.Episodes.FirstOrDefault(e => e.EpisodeNumber == number);
                    if (episode == null)
                    {
                        episode = new Episode { EpisodeNumber = number };
                        season.Episodes.Add(episode);
                    }

                    episode.Name = $"S{seasonNumber:00}E{number:00}";
                    episode.AirDate = season.AirDate.Value.AddDays(7 * (number - 1));
                    episode.Runtime = 47;
                    // Isı haritası testinin bakacağı değer: sabit değil ki
                    // "hepsi aynı renk" durumu gerçek bir dağılımı gizlemesin.
                    episode.TmdbVoteAverage = 7.5 + (number % 5) * 0.3;
                    episode.TmdbVoteCount = 100 + number;
                }
            }

            await context.SaveChangesAsync();
        }
    }
}
