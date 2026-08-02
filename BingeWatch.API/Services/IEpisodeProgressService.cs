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

        /// <summary>
        /// "Bu bölümde şu dakikada kaldım" işareti. Bölüm izlenmiş olarak
        /// işaretliyse reddedilir (<c>false</c>): izlenen bölümde yarıda kalınmış
        /// olamaz. Dakika negatifse ya da bölüm süresini aşıyorsa da reddedilir.
        /// </summary>
        Task<bool> SetBookmarkAsync(string userId, int episodeId, int positionMinutes);

        /// <summary>İşareti kaldırır. Kayıt yoksa <c>false</c>.</summary>
        Task<bool> ClearBookmarkAsync(string userId, int episodeId);

        /// <summary>Bölümde kalınan dakika; işaret yoksa <c>null</c>.</summary>
        Task<int?> GetBookmarkAsync(string userId, int episodeId);

        /// <summary>"Sırada ne var" paneli — kullanıcının listesindeki her aktif dizi için sıradaki bölüm.</summary>
        Task<List<NextEpisodeDto>> GetNextUpAsync(string userId);

        /// <summary>Kullanıcının listesindeki dizilerin yaklaşan bölümleri (takvim).</summary>
        Task<List<UpcomingEpisodeDto>> GetUpcomingEpisodesAsync(string userId, int daysAhead);
    }
}
