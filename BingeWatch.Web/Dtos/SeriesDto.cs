using System.Text.Json.Serialization;

namespace BingeWatch.Web.Dtos
{
    public class SeriesDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Overview { get; set; }
        public string PosterPath { get; set; }
        public string FirstAirDate { get; set; }
        public ExternalIdsDto ExternalIds { get; set; }
        public string ImdbId { get; set; }
        public bool IsInWatchList { get; set; } = false;

        public void MapExternalIds()
        {
            if (ExternalIds != null)
                ImdbId = ExternalIds.ImdbId;
        }
    }

    public class ExternalIdsDto
    {
        [JsonPropertyName("imdb_id")]
        public string ImdbId { get; set; }
    }
}
