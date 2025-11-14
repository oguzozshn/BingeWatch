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
    }
}
