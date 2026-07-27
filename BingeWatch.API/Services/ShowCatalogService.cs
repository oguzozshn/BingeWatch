using Microsoft.EntityFrameworkCore;
using BingeWatch.API.Clients;
using BingeWatch.API.Data;
using BingeWatch.API.Models;

namespace BingeWatch.API.Services
{
    /// <summary>
    /// TMDb'yi yerel Show/Season/Episode katalogu ile eşitler. Bölüm bazlı takip
    /// ve toplu istatistikler bu yerel kopya üzerinde çalışır; her istekte TMDb'ye
    /// gidilmez (bkz. Roadmap §4 "Kritik karar").
    /// </summary>
    public class ShowCatalogService : IShowCatalogService
    {
        // Biten diziler tekrar bölüm üretmez; devam edenler daha sık kontrol edilir.
        private static readonly TimeSpan EndedShowTtl = TimeSpan.FromDays(7);
        private static readonly TimeSpan OngoingShowTtl = TimeSpan.FromHours(12);

        private readonly BingeOnDbContext _context;
        private readonly TmdbClient _tmdbClient;
        private readonly ILogger<ShowCatalogService> _logger;

        public ShowCatalogService(BingeOnDbContext context, TmdbClient tmdbClient, ILogger<ShowCatalogService> logger)
        {
            _context = context;
            _tmdbClient = tmdbClient;
            _logger = logger;
        }

        public async Task<Show?> GetOrSyncShowAsync(int tmdbId, bool forceSync = false)
        {
            var show = await _context.Shows
                .Include(s => s.Seasons).ThenInclude(se => se.Episodes)
                .Include(s => s.Genres)
                .Include(s => s.Networks)
                .FirstOrDefaultAsync(s => s.TmdbId == tmdbId);

            if (show != null && !forceSync && !IsStale(show))
                return show;

            return await SyncShowAsync(tmdbId, show);
        }

        public async Task<int> SyncStaleOngoingShowsAsync(CancellationToken cancellationToken = default)
        {
            var cutoff = DateTime.UtcNow - OngoingShowTtl;

            var staleShowIds = await _context.Shows
                .Where(s => s.TmdbStatus != "Ended" && s.TmdbStatus != "Canceled")
                .Where(s => s.LastSyncedAt < cutoff)
                .Select(s => s.TmdbId)
                .ToListAsync(cancellationToken);

            var syncedCount = 0;
            foreach (var tmdbId in staleShowIds)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var existing = await _context.Shows
                        .Include(s => s.Seasons).ThenInclude(se => se.Episodes)
                        .Include(s => s.Genres)
                        .Include(s => s.Networks)
                        .FirstOrDefaultAsync(s => s.TmdbId == tmdbId, cancellationToken);

                    await SyncShowAsync(tmdbId, existing);
                    syncedCount++;
                }
                catch (Exception ex)
                {
                    // Tek bir dizinin TMDb hatası tüm senkron turunu durdurmamalı.
                    _logger.LogError(ex, "Failed to sync show {TmdbId} during background sync", tmdbId);
                }
            }

            return syncedCount;
        }

        private bool IsStale(Show show)
        {
            if (show.LastSyncedAt == default)
                return true;

            var ttl = (show.TmdbStatus == "Ended" || show.TmdbStatus == "Canceled")
                ? EndedShowTtl
                : OngoingShowTtl;

            return DateTime.UtcNow - show.LastSyncedAt > ttl;
        }

        private async Task<Show?> SyncShowAsync(int tmdbId, Show? existing)
        {
            var details = await _tmdbClient.GetShowDetailsAsync(tmdbId);
            if (details == null)
            {
                _logger.LogWarning("TMDb has no show with id {TmdbId}", tmdbId);
                return existing; // TMDb geçici hata verdiyse elimizdeki en son veriyi koru
            }

            var show = existing ?? new Show { TmdbId = tmdbId };

            show.Name = details.Name;
            show.Overview = details.Overview;
            show.PosterPath = details.PosterPath;
            show.BackdropPath = details.BackdropPath;
            show.FirstAirDate = details.FirstAirDate;
            show.TmdbStatus = details.Status;
            show.VoteAverage = details.VoteAverage;
            show.VoteCount = details.VoteCount;
            show.ImdbId = details.ExternalIds?.ImdbId ?? show.ImdbId;
            show.LastSyncedAt = DateTime.UtcNow;

            if (show.Id == 0)
                _context.Shows.Add(show);

            await SyncGenresAsync(show, details.Genres);
            await SyncNetworksAsync(show, details.Networks);

            // Show.Id'ye ihtiyaç duyan Season satırları için (yeni dizilerde) önce kaydet.
            await _context.SaveChangesAsync();

            foreach (var seasonSummary in details.Seasons.Where(s => s.SeasonNumber > 0 && s.EpisodeCount > 0))
            {
                var season = show.Seasons.FirstOrDefault(s => s.SeasonNumber == seasonSummary.SeasonNumber);
                if (season == null)
                {
                    season = new Season { ShowId = show.Id, SeasonNumber = seasonSummary.SeasonNumber };
                    show.Seasons.Add(season);
                    _context.Seasons.Add(season);
                }

                season.Name = seasonSummary.Name;
                season.Overview = seasonSummary.Overview;
                season.PosterPath = seasonSummary.PosterPath;
                season.AirDate = seasonSummary.AirDate;
                season.EpisodeCount = seasonSummary.EpisodeCount;
            }

            await _context.SaveChangesAsync();

            foreach (var season in show.Seasons)
            {
                var seasonDetails = await _tmdbClient.GetSeasonDetailsAsync(tmdbId, season.SeasonNumber);
                if (seasonDetails == null)
                    continue;

                foreach (var ep in seasonDetails.Episodes)
                {
                    var episode = season.Episodes.FirstOrDefault(e => e.EpisodeNumber == ep.EpisodeNumber);
                    if (episode == null)
                    {
                        episode = new Episode { SeasonId = season.Id, EpisodeNumber = ep.EpisodeNumber };
                        season.Episodes.Add(episode);
                        _context.Episodes.Add(episode);
                    }

                    episode.Name = ep.Name;
                    episode.Overview = ep.Overview;
                    episode.StillPath = ep.StillPath;
                    episode.AirDate = ep.AirDate;
                    episode.Runtime = ep.Runtime;
                    episode.TmdbVoteAverage = ep.VoteAverage;
                    episode.TmdbVoteCount = ep.VoteCount;
                }
            }

            await _context.SaveChangesAsync();

            return show;
        }

        /// <summary>
        /// Dizinin türlerini TMDb'deki listeye eşitler. Tür satırları paylaşımlı ve
        /// TMDb id'siyle anahtarlı; yeni görülen tür ilk kez burada oluşturulur.
        /// </summary>
        private async Task SyncGenresAsync(Show show, List<TmdbGenre> tmdbGenres)
        {
            if (tmdbGenres.Count == 0)
            {
                show.Genres.Clear();
                return;
            }

            var ids = tmdbGenres.Select(g => g.Id).ToList();
            var known = await _context.Genres.Where(g => ids.Contains(g.Id)).ToListAsync();

            foreach (var tmdbGenre in tmdbGenres)
            {
                var genre = known.FirstOrDefault(g => g.Id == tmdbGenre.Id);
                if (genre == null)
                {
                    genre = new Genre { Id = tmdbGenre.Id, Name = tmdbGenre.Name };
                    _context.Genres.Add(genre);
                    known.Add(genre);
                }
                else
                {
                    genre.Name = tmdbGenre.Name;
                }
            }

            show.Genres.RemoveAll(g => !ids.Contains(g.Id));
            foreach (var genre in known.Where(g => show.Genres.All(x => x.Id != g.Id)))
                show.Genres.Add(genre);
        }

        private async Task SyncNetworksAsync(Show show, List<TmdbNetwork> tmdbNetworks)
        {
            if (tmdbNetworks.Count == 0)
            {
                show.Networks.Clear();
                return;
            }

            var ids = tmdbNetworks.Select(n => n.Id).ToList();
            var known = await _context.Networks.Where(n => ids.Contains(n.Id)).ToListAsync();

            foreach (var tmdbNetwork in tmdbNetworks)
            {
                var network = known.FirstOrDefault(n => n.Id == tmdbNetwork.Id);
                if (network == null)
                {
                    network = new Network
                    {
                        Id = tmdbNetwork.Id,
                        Name = tmdbNetwork.Name,
                        LogoPath = tmdbNetwork.LogoPath
                    };
                    _context.Networks.Add(network);
                    known.Add(network);
                }
                else
                {
                    network.Name = tmdbNetwork.Name;
                    network.LogoPath = tmdbNetwork.LogoPath;
                }
            }

            show.Networks.RemoveAll(n => !ids.Contains(n.Id));
            foreach (var network in known.Where(n => show.Networks.All(x => x.Id != n.Id)))
                show.Networks.Add(network);
        }
    }
}
