using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BingeWatch.API.Models
{
    public class TmdbSeriesResult
    {
        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("results")]
        public List<SeriesItem> Results { get; set; }
    }

    public class SeriesItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("overview")]
        public string Overview { get; set; }

        [JsonPropertyName("poster_path")]
        public string PosterPath { get; set; }

        [JsonPropertyName("first_air_date")]
        public DateTime? FirstAirDate { get; set; }

        [JsonPropertyName("vote_average")]
        public double VoteAverage { get; set; }

        [JsonPropertyName("vote_count")]
        public int VoteCount { get; set; }
    }

    /// <summary>TMDb GET /search/person yanıtı.</summary>
    public class TmdbPersonSearchResult
    {
        [JsonPropertyName("results")]
        public List<TmdbPersonItem> Results { get; set; } = new();
    }

    public class TmdbPersonItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("profile_path")]
        public string? ProfilePath { get; set; }

        [JsonPropertyName("known_for_department")]
        public string? KnownForDepartment { get; set; }

        [JsonPropertyName("popularity")]
        public double Popularity { get; set; }
    }

    /// <summary>TMDb GET /person/{id}/tv_credits yanıtı.</summary>
    public class TmdbPersonTvCreditsResponse
    {
        [JsonPropertyName("cast")]
        public List<TmdbPersonTvCredit> Cast { get; set; } = new();

        [JsonPropertyName("crew")]
        public List<TmdbPersonTvCredit> Crew { get; set; } = new();
    }

    public class TmdbPersonTvCredit
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; set; }

        [JsonPropertyName("first_air_date")]
        public DateTime? FirstAirDate { get; set; }

        [JsonPropertyName("vote_average")]
        public double VoteAverage { get; set; }

        /// <summary>Oyuncu kredilerinde canlandırdığı karakter.</summary>
        [JsonPropertyName("character")]
        public string? Character { get; set; }

        /// <summary>Ekip kredilerinde görevi ("Director", "Writer"...).</summary>
        [JsonPropertyName("job")]
        public string? Job { get; set; }

        /// <summary>Kişinin o dizideki bölüm sayısı; anlamlı işleri öne almak için.</summary>
        [JsonPropertyName("episode_count")]
        public int EpisodeCount { get; set; }
    }
}
