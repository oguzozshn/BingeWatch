using BingeWatch.API.Models;

namespace BingeWatch.API.Services
{
    public interface IShowCatalogService
    {
        /// <summary>
        /// Diziyi (sezon + bölüm dahil) yerel katalogdan döner; hiç senkronize
        /// edilmemişse veya bayatlamışsa önce TMDb'den güncelleyip öyle döner.
        /// TMDb'de böyle bir dizi yoksa <c>null</c>.
        /// </summary>
        Task<Show?> GetOrSyncShowAsync(int tmdbId, bool forceSync = false);

        /// <summary>
        /// Katalogdaki "devam ediyor" durumundaki dizileri TMDb ile eşitler.
        /// <see cref="TmdbSyncService"/> tarafından periyodik çağrılır.
        /// </summary>
        Task<int> SyncStaleOngoingShowsAsync(CancellationToken cancellationToken = default);
    }
}
