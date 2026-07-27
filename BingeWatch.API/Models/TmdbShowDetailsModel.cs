using System.Text.Json.Serialization;

namespace BingeWatch.API.Models
{
    /// <summary>TMDb GET /tv/{id}?append_to_response=external_ids yanıtı.</summary>
    public class TmdbShowDetailsResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("overview")]
        public string Overview { get; set; } = string.Empty;

        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; set; }

        [JsonPropertyName("backdrop_path")]
        public string? BackdropPath { get; set; }

        [JsonPropertyName("first_air_date")]
        public DateTime? FirstAirDate { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("vote_average")]
        public double VoteAverage { get; set; }

        [JsonPropertyName("vote_count")]
        public int VoteCount { get; set; }

        [JsonPropertyName("seasons")]
        public List<TmdbSeasonSummary> Seasons { get; set; } = new();

        [JsonPropertyName("genres")]
        public List<TmdbGenre> Genres { get; set; } = new();

        [JsonPropertyName("networks")]
        public List<TmdbNetwork> Networks { get; set; } = new();

        [JsonPropertyName("external_ids")]
        public TmdbExternalIds? ExternalIds { get; set; }
    }

    public class TmdbGenre
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class TmdbNetwork
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("logo_path")]
        public string? LogoPath { get; set; }
    }

    /// <summary>TMDb GET /genre/tv/list yanıtı.</summary>
    public class TmdbGenreListResponse
    {
        [JsonPropertyName("genres")]
        public List<TmdbGenre> Genres { get; set; } = new();
    }

    public class TmdbSeasonSummary
    {
        [JsonPropertyName("season_number")]
        public int SeasonNumber { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("overview")]
        public string? Overview { get; set; }

        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; set; }

        [JsonPropertyName("air_date")]
        public DateTime? AirDate { get; set; }

        [JsonPropertyName("episode_count")]
        public int EpisodeCount { get; set; }
    }

    public class TmdbExternalIds
    {
        [JsonPropertyName("imdb_id")]
        public string? ImdbId { get; set; }
    }

    /// <summary>TMDb GET /tv/{id}/season/{season_number} yanıtı.</summary>
    public class TmdbSeasonDetailsResponse
    {
        [JsonPropertyName("season_number")]
        public int SeasonNumber { get; set; }

        [JsonPropertyName("episodes")]
        public List<TmdbEpisodeDetail> Episodes { get; set; } = new();
    }

    public class TmdbEpisodeDetail
    {
        [JsonPropertyName("episode_number")]
        public int EpisodeNumber { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("overview")]
        public string? Overview { get; set; }

        [JsonPropertyName("still_path")]
        public string? StillPath { get; set; }

        [JsonPropertyName("air_date")]
        public DateTime? AirDate { get; set; }

        [JsonPropertyName("runtime")]
        public int? Runtime { get; set; }

        [JsonPropertyName("vote_average")]
        public double VoteAverage { get; set; }

        [JsonPropertyName("vote_count")]
        public int VoteCount { get; set; }
    }
}
