using BingeWatch.API.Dtos;

namespace BingeWatch.API.Services
{
    public interface IGameService
    {
        /// <summary>
        /// Yeni bir el hazırlar. Havuz kurulamazsa (TMDb ulaşılamıyor)
        /// <c>null</c> döner.
        /// </summary>
        Task<GameRoundDto?> GetRoundAsync();
    }

    /// <summary>
    /// "Hangisinin puanı yüksek?" oyununun el üreticisi.
    ///
    /// Havuz <b>yerel katalogdan değil TMDb popüler listesinden</b> geliyor:
    /// yerel katalog yalnızca dokunulmuş dizileri içeriyor, yeni bir kurulumda
    /// (Docker, Pi demosu) bomboş olurdu ve oyun hiç açılmazdı.
    /// </summary>
    public class GameService : IGameService
    {
        /// <summary>
        /// Kaç sayfa popüler dizi havuza girsin. Tek sayfa (20 dizi) çabuk
        /// tekrara düşüyor; TMDb yanıtları zaten cache'li.
        /// </summary>
        private const int PoolPages = 3;

        /// <summary>
        /// Az oylu diziler puanı anlamsız kılıyor (tek 10'luk oy "10.0" yapar).
        /// Keşif sayfasındaki <c>vote_count.gte=50</c> eşiğiyle aynı gerekçe.
        /// </summary>
        private const int MinVoteCount = 50;

        private readonly ITmdbService _tmdb;

        public GameService(ITmdbService tmdb) => _tmdb = tmdb;

        public async Task<GameRoundDto?> GetRoundAsync()
        {
            var pool = new List<SeriesDto>();
            for (var page = 1; page <= PoolPages; page++)
                pool.AddRange(await _tmdb.GetPopularSeriesAsync(page));

            var candidates = pool
                .Where(s => s.VoteCount >= MinVoteCount && s.VoteAverage > 0)
                .GroupBy(s => s.Id)
                .Select(g => g.First())
                .ToList();

            if (candidates.Count < 2)
                return null;

            var first = candidates[Random.Shared.Next(candidates.Count)];

            // Aynı dizi iki kez çıkmasın; ayrıca puanı birbirine çok yakın
            // çiftler tahmin değil kura oluyor, en fazla birkaç kez deniyoruz.
            SeriesDto second;
            var attempts = 0;
            do
            {
                second = candidates[Random.Shared.Next(candidates.Count)];
                attempts++;
            }
            while ((second.Id == first.Id
                    || Math.Abs(second.VoteAverage - first.VoteAverage) < 0.1)
                   && attempts < 20);

            if (second.Id == first.Id)
                return null;

            var isTie = Math.Abs(first.VoteAverage - second.VoteAverage) < 0.001;

            return new GameRoundDto
            {
                Left = ToContender(first),
                Right = ToContender(second),
                WinnerTmdbId = first.VoteAverage >= second.VoteAverage ? first.Id : second.Id,
                IsTie = isTie
            };
        }

        private static GameContenderDto ToContender(SeriesDto s) => new()
        {
            TmdbId = s.Id,
            Name = s.Name,
            PosterPath = string.IsNullOrWhiteSpace(s.PosterPath) ? null : s.PosterPath,
            FirstAirYear = s.FirstAirDate?.Year,
            VoteAverage = s.VoteAverage
        };
    }
}
