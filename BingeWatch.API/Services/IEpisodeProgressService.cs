using BingeWatch.API.Dtos;

namespace BingeWatch.API.Services
{
    public interface IEpisodeProgressService
    {
        Task<bool> SetEpisodeWatchedAsync(string userId, int episodeId, bool watched);
        Task<int> SetSeasonWatchedAsync(string userId, int showTmdbId, int seasonNumber, bool watched);
        Task<int> SetWatchedUpToAsync(string userId, int showTmdbId, int episodeId);

        Task<ShowProgressDto?> GetShowProgressAsync(string userId, int showTmdbId);

        /// <summary>Bir dizideki, kullanıcının izlediği bölümlerin yerel id kümesi.</summary>
        Task<HashSet<int>> GetWatchedEpisodeIdsAsync(string userId, int showTmdbId);

        /// <summary>
        /// Bölüme bir yeniden izleme kaydı ekler. Bölüm ilk kez izlenmiş olmalı;
        /// değilse <c>null</c> döner. Dönen değer yeni rewatch sayısıdır.
        /// </summary>
        Task<int?> AddRewatchAsync(string userId, int episodeId);

        /// <summary>
        /// Son yeniden izleme kaydını siler (ilk izlemeye dokunmaz). Silinecek
        /// rewatch yoksa <c>null</c> döner. Dönen değer kalan rewatch sayısıdır.
        /// </summary>
        Task<int?> RemoveLastRewatchAsync(string userId, int episodeId);

        /// <summary>Bir bölümün kaç kez yeniden izlendiği (ilk izleme hariç).</summary>
        Task<int> GetRewatchCountAsync(string userId, int episodeId);

        /// <summary>"Sırada ne var" paneli — kullanıcının listesindeki her aktif dizi için sıradaki bölüm.</summary>
        Task<List<NextEpisodeDto>> GetNextUpAsync(string userId);

        /// <summary>Kullanıcının listesindeki dizilerin yaklaşan bölümleri (takvim).</summary>
        Task<List<UpcomingEpisodeDto>> GetUpcomingEpisodesAsync(string userId, int daysAhead);
    }
}
