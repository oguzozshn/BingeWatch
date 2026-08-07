using BingeWatch.API.Dtos;
using BingeWatch.API.Services;
using Xunit;

namespace BingeWatch.Tests
{
    public class GameServiceTests
    {
        /// <summary>
        /// Havuzu testin kontrolünde tutan sahte TMDb servisi; gerçek ağa
        /// çıkılmıyor, dolayısıyla sonuçlar dışarıda değişen veriye bağlı değil.
        /// </summary>
        private sealed class FakeTmdbService : ITmdbService
        {
            private readonly List<SeriesDto> _series;

            public FakeTmdbService(params SeriesDto[] series) => _series = series.ToList();

            public Task<List<SeriesDto>> GetPopularSeriesAsync(int page) =>
                // Sayfa 1 havuzu veriyor, kalan sayfalar boş: servis üç sayfa
                // istiyor ama tekrar eden veri sonucu bozmamalı.
                Task.FromResult(page == 1 ? _series.ToList() : new List<SeriesDto>());

            public Task<List<SeriesDto>> SearchSeriesAsync(string query, int page) =>
                Task.FromResult(new List<SeriesDto>());

            public Task<List<SeriesDto>> GetSimilarSeriesAsync(int tmdbId, int page = 1) =>
                Task.FromResult(new List<SeriesDto>());
        }

        private static SeriesDto Series(int id, double vote, int voteCount = 500) => new()
        {
            Id = id,
            Name = $"Dizi {id}",
            PosterPath = $"/{id}.jpg",
            FirstAirDate = new DateTime(2020, 1, 1),
            VoteAverage = vote,
            VoteCount = voteCount
        };

        [Fact]
        public async Task GetRoundAsync_ReturnsTwoDistinctShows()
        {
            var service = new GameService(new FakeTmdbService(
                Series(1, 8.5), Series(2, 7.0), Series(3, 6.0)));

            var round = await service.GetRoundAsync();

            Assert.NotNull(round);
            Assert.NotEqual(round!.Left.TmdbId, round.Right.TmdbId);
        }

        [Fact]
        public async Task GetRoundAsync_PicksHigherRatedAsWinner()
        {
            var service = new GameService(new FakeTmdbService(
                Series(1, 9.1), Series(2, 5.2)));

            var round = await service.GetRoundAsync();

            Assert.NotNull(round);
            var winner = round!.Left.TmdbId == round.WinnerTmdbId ? round.Left : round.Right;
            var loser = round.Left.TmdbId == round.WinnerTmdbId ? round.Right : round.Left;
            Assert.True(winner.VoteAverage > loser.VoteAverage);
        }

        /// <summary>
        /// Tek 10'luk oy almış bir dizi "10.0" görünüyor; böyle bir çift
        /// tahmin değil kura olurdu. Keşif sayfasındaki eşikle aynı gerekçe.
        /// </summary>
        [Fact]
        public async Task GetRoundAsync_IgnoresShowsWithTooFewVotes()
        {
            var service = new GameService(new FakeTmdbService(
                Series(1, 8.0), Series(2, 7.0), Series(99, 10.0, voteCount: 3)));

            // Havuzda üç dizi var ama biri elenmeli; birçok denemede hiç çıkmamalı.
            for (var i = 0; i < 25; i++)
            {
                var round = await service.GetRoundAsync();
                Assert.NotNull(round);
                Assert.NotEqual(99, round!.Left.TmdbId);
                Assert.NotEqual(99, round.Right.TmdbId);
            }
        }

        [Fact]
        public async Task GetRoundAsync_ReturnsNullWhenPoolTooSmall()
        {
            var tekDizi = new GameService(new FakeTmdbService(Series(1, 8.0)));
            Assert.Null(await tekDizi.GetRoundAsync());

            var bosHavuz = new GameService(new FakeTmdbService());
            Assert.Null(await bosHavuz.GetRoundAsync());
        }

        /// <summary>Puanı sıfır olan kayıtlar (TMDb'de oylanmamış) oyuna girmez.</summary>
        [Fact]
        public async Task GetRoundAsync_IgnoresUnratedShows()
        {
            var service = new GameService(new FakeTmdbService(
                Series(1, 0), Series(2, 0)));

            Assert.Null(await service.GetRoundAsync());
        }

        private static SeriesDto Series(int id, double vote, int year, int voteCount = 500) => new()
        {
            Id = id,
            Name = $"Dizi {id}",
            PosterPath = $"/{id}.jpg",
            FirstAirDate = new DateTime(year, 1, 1),
            VoteAverage = vote,
            VoteCount = voteCount
        };

        /// <summary>Havuzu dört ayrı yılla kuruyoruz ki her soru tipi üretilebilsin.</summary>
        private static GameService TriviaService() => new(new FakeTmdbService(
            Series(1, 8.5, 2001), Series(2, 7.4, 2005),
            Series(3, 6.9, 2012), Series(4, 8.1, 2019),
            Series(5, 7.7, 2021)));

        [Fact]
        public async Task GetTriviaAsync_ReturnsFourDistinctOptionsWithValidAnswer()
        {
            var service = TriviaService();

            // Tip rastgele seçiliyor; birçok kez çekip hepsinin tutarlı
            // olduğunu görüyoruz.
            for (var i = 0; i < 40; i++)
            {
                var q = await service.GetTriviaAsync();

                Assert.NotNull(q);
                Assert.False(string.IsNullOrWhiteSpace(q!.Question));
                Assert.Equal(4, q.Options.Count);
                Assert.Equal(4, q.Options.Distinct().Count());
                Assert.InRange(q.CorrectIndex, 0, q.Options.Count - 1);
            }
        }

        [Fact]
        public async Task GetTriviaAsync_ReturnsNullWhenPoolTooSmall()
        {
            var service = new GameService(new FakeTmdbService(
                Series(1, 8.0, 2001), Series(2, 7.0, 2002), Series(3, 6.0, 2003)));

            Assert.Null(await service.GetTriviaAsync());
        }

        /// <summary>
        /// Poster sorusunda görselin sorulan diziye ait olması şart; şıklardan
        /// biri de o dizinin adı olmalı.
        /// </summary>
        [Fact]
        public async Task GetTriviaAsync_PosterQuestionMatchesItsAnswer()
        {
            var service = TriviaService();
            var seen = false;

            for (var i = 0; i < 60 && !seen; i++)
            {
                var q = await service.GetTriviaAsync();
                if (q?.PosterPath == null)
                    continue;

                seen = true;
                // "/3.jpg" → "Dizi 3"
                var id = q.PosterPath.Trim('/').Replace(".jpg", string.Empty);
                Assert.Equal($"Dizi {id}", q.Options[q.CorrectIndex]);
            }

            Assert.True(seen, "poster sorusu hiç üretilmedi");
        }

        [Fact]
        public async Task GetRoundAsync_CarriesPosterAndYear()
        {
            var service = new GameService(new FakeTmdbService(
                Series(1, 8.5), Series(2, 6.5)));

            var round = await service.GetRoundAsync();

            Assert.NotNull(round);
            Assert.False(string.IsNullOrWhiteSpace(round!.Left.Name));
            Assert.Equal(2020, round.Left.FirstAirYear);
            Assert.NotNull(round.Left.PosterPath);
        }
    }
}
