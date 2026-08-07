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

        /// <summary>
        /// Katalog verisinden üretilmiş bir trivia sorusu. Havuz yetersizse
        /// <c>null</c> döner.
        /// </summary>
        Task<TriviaQuestionDto?> GetTriviaAsync();
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

        /// <summary>
        /// Popüler listesinden tekilleştirilmiş aday havuzu. Az oylu ve
        /// puansız diziler eleniyor.
        /// </summary>
        private async Task<List<SeriesDto>> BuildPoolAsync()
        {
            var pool = new List<SeriesDto>();
            for (var page = 1; page <= PoolPages; page++)
                pool.AddRange(await _tmdb.GetPopularSeriesAsync(page));

            return pool
                .Where(s => s.VoteCount >= MinVoteCount && s.VoteAverage > 0)
                .GroupBy(s => s.Id)
                .Select(g => g.First())
                .ToList();
        }

        public async Task<GameRoundDto?> GetRoundAsync()
        {
            var candidates = await BuildPoolAsync();

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

        /// <summary>
        /// Soru saklanmıyor, havuzdan üretiliyor. Üç tip var ve hepsi popüler
        /// listesindeki alanlarla (ad, yıl, poster, puan) kuruluyor — yerel
        /// katalog gerektirmediği için boş bir kurulumda da çalışır.
        /// </summary>
        public async Task<TriviaQuestionDto?> GetTriviaAsync()
        {
            var candidates = await BuildPoolAsync();
            if (candidates.Count < 4)
                return null; // dört şık kurulamıyor

            // Bazı tipler belirli havuzlarda soru üretemiyor (ör. seçilen dört
            // dizi aynı yılda başlamışsa "hangisi en eski" tek cevaplı olmaz).
            // Boş dönmek yerine birkaç kez deniyoruz; havuz sabit olduğu için
            // ek TMDb isteği doğmuyor.
            for (var attempt = 0; attempt < 6; attempt++)
            {
                var question = Random.Shared.Next(3) switch
                {
                    0 => BuildPosterQuestion(candidates),
                    1 => BuildYearQuestion(candidates),
                    _ => BuildOldestQuestion(candidates)
                };

                if (question != null)
                    return question;
            }

            return null;
        }

        /// <summary>"Bu poster hangi dizinin?" — şıklar diğer dizilerin adları.</summary>
        private static TriviaQuestionDto? BuildPosterQuestion(List<SeriesDto> pool)
        {
            var withPoster = pool.Where(s => !string.IsNullOrWhiteSpace(s.PosterPath)).ToList();
            if (withPoster.Count < 4)
                return null;

            var answer = withPoster[Random.Shared.Next(withPoster.Count)];
            var options = PickOptions(withPoster, answer, s => s.Name);

            return new TriviaQuestionDto
            {
                Question = "Bu poster hangi diziye ait?",
                PosterPath = answer.PosterPath,
                Options = options.Select(s => s.Name).ToList(),
                CorrectIndex = options.FindIndex(s => s.Id == answer.Id)
            };
        }

        /// <summary>"X hangi yıl yayına başladı?" — şıklar yakın yıllar.</summary>
        private static TriviaQuestionDto? BuildYearQuestion(List<SeriesDto> pool)
        {
            var dated = pool.Where(s => s.FirstAirDate.HasValue).ToList();
            if (dated.Count == 0)
                return null;

            var answer = dated[Random.Shared.Next(dated.Count)];
            var year = answer.FirstAirDate!.Value.Year;

            // Çeldiriciler doğru yılın etrafından: rastgele yıllar soruyu
            // tahmin değil eleme oyununa çevirirdi.
            var years = new HashSet<int> { year };
            var offsets = new[] { -4, -3, -2, -1, 1, 2, 3, 4 }.OrderBy(_ => Random.Shared.Next()).ToList();
            foreach (var offset in offsets)
            {
                if (years.Count == 4)
                    break;
                years.Add(year + offset);
            }

            var options = years.OrderBy(_ => Random.Shared.Next()).ToList();

            return new TriviaQuestionDto
            {
                Question = $"\"{answer.Name}\" hangi yıl yayına başladı?",
                Options = options.Select(y => y.ToString()).ToList(),
                CorrectIndex = options.IndexOf(year),
                Explanation = $"{answer.Name} — {year}"
            };
        }

        /// <summary>"Hangisi en eski?" — dört dizi arasından.</summary>
        private static TriviaQuestionDto? BuildOldestQuestion(List<SeriesDto> pool)
        {
            var dated = pool.Where(s => s.FirstAirDate.HasValue).ToList();
            if (dated.Count < 4)
                return null;

            var picks = dated.OrderBy(_ => Random.Shared.Next()).Take(4).ToList();

            // Aynı yılda başlayan iki dizi olursa soru tek cevaplı olmaz.
            if (picks.Select(s => s.FirstAirDate!.Value.Year).Distinct().Count() != picks.Count)
                return null;

            var oldest = picks.OrderBy(s => s.FirstAirDate!.Value).First();

            return new TriviaQuestionDto
            {
                Question = "Bu dizilerden hangisi en önce yayına başladı?",
                Options = picks.Select(s => s.Name).ToList(),
                CorrectIndex = picks.FindIndex(s => s.Id == oldest.Id),
                Explanation = $"{oldest.Name} — {oldest.FirstAirDate!.Value.Year}"
            };
        }

        /// <summary>
        /// Doğru cevap + üç çeldirici, karışık sırada. Çeldiriciler
        /// <paramref name="key"/>'e göre tekilleştiriliyor: aynı adı taşıyan
        /// iki şık soruyu cevapsız bırakırdı.
        /// </summary>
        private static List<SeriesDto> PickOptions(
            List<SeriesDto> pool, SeriesDto answer, Func<SeriesDto, string> key)
        {
            var options = new List<SeriesDto> { answer };
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { key(answer) };

            foreach (var candidate in pool.OrderBy(_ => Random.Shared.Next()))
            {
                if (options.Count == 4)
                    break;
                if (candidate.Id == answer.Id || !used.Add(key(candidate)))
                    continue;
                options.Add(candidate);
            }

            return options.OrderBy(_ => Random.Shared.Next()).ToList();
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
