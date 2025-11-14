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
    }
}
