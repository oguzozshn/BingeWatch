using BingeWatch.API.Dtos;

namespace BingeWatch.API.Services
{
    public interface IDiscoverService
    {
        /// <summary>
        /// Filtreli keşif. <see cref="DiscoverQuery.Status"/> doluysa arama isteği
        /// yapanın kütüphanesinde yapılır (yerel), boşsa TMDb kataloğunda.
        /// </summary>
        Task<DiscoverResultDto> DiscoverAsync(DiscoverQuery query, string? viewerId);

        /// <summary>Filtre panelinin tür listesi — TMDb'nin tam listesi.</summary>
        Task<List<GenreDto>> GetGenresAsync();

        /// <summary>
        /// Filtre panelinin platform listesi. TMDb'de "tüm kanallar" uç noktası yok;
        /// yerel katalogda görülenler ile bilinen büyük platformlar birleştirilir.
        /// </summary>
        Task<List<NetworkDto>> GetNetworksAsync();

        /// <summary>Dizi + kişi araması tek yanıtta.</summary>
        Task<SearchResultDto> SearchAsync(string query, bool includePeople);

        /// <summary>Kişinin dizileri; TMDb'de kişi yoksa <c>null</c>.</summary>
        Task<PersonCreditsDto?> GetPersonCreditsAsync(int personId);
    }
}
