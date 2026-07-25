using System.Text.Json.Serialization;

namespace BingeWatch.Web.Models
{
    public class OmdbEpisodeRatingModel
    {
        public string Title { get; set; }
        public string Season { get; set; }
        public string Episode { get; set; }
        public string imdbRating { get; set; }
    }

    public class OmdbSeasonResponse
    {
        public string Title { get; set; }
        public string Season { get; set; }
        public List<OmdbEpisodeRatingModel> Episodes { get; set; }
    }

    public class OmdbShowModel
    {
        public string Title { get; set; }
        public string Year { get; set; }
        public string Poster { get; set; }
        public string imdbRating { get; set; }
        public string imdbVotes { get; set; }
        public string totalSeasons { get; set; }
    }

    public class TmdbShowDetailsModel
    {
        public string Name { get; set; }

        [JsonPropertyName("first_air_date")]
        public string FirstAirDate { get; set; }

        [JsonPropertyName("poster_path")]
        public string PosterPath { get; set; }

        [JsonPropertyName("vote_average")]
        public double VoteAverage { get; set; }

        [JsonPropertyName("vote_count")]
        public int VoteCount { get; set; }

        public List<TmdbSeasonSummaryModel> Seasons { get; set; }
    }

    public class TmdbSeasonSummaryModel
    {
        [JsonPropertyName("season_number")]
        public int SeasonNumber { get; set; }

        [JsonPropertyName("episode_count")]
        public int EpisodeCount { get; set; }
    }

    public class TmdbSeasonResponse
    {
        public List<TmdbEpisodeModel> Episodes { get; set; }
    }

    public class TmdbEpisodeModel
    {
        public string Name { get; set; }

        [JsonPropertyName("season_number")]
        public int SeasonNumber { get; set; }

        [JsonPropertyName("episode_number")]
        public int EpisodeNumber { get; set; }

        [JsonPropertyName("vote_average")]
        public double VoteAverage { get; set; }
    }
}
