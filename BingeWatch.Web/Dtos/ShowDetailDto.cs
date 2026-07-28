namespace BingeWatch.Web.Dtos
{
    public class ShowDetailDto
    {
        public int TmdbId { get; set; }
        public string? ImdbId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Overview { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
        public string? BackdropPath { get; set; }
        public DateTime? FirstAirDate { get; set; }
        public string? Status { get; set; }
        public double VoteAverage { get; set; }
        public int VoteCount { get; set; }
        public List<SeasonDetailDto> Seasons { get; set; } = new();
    }

    public class SeasonDetailDto
    {
        public int SeasonNumber { get; set; }
        public string? Name { get; set; }
        public DateTime? AirDate { get; set; }
        public List<EpisodeDetailDto> Episodes { get; set; } = new();
    }

    public class EpisodeDetailDto
    {
        public int Id { get; set; }
        public int EpisodeNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? StillPath { get; set; }
        public DateTime? AirDate { get; set; }
        public int? Runtime { get; set; }
        public double TmdbVoteAverage { get; set; }
        public bool Watched { get; set; }
        public DateTime? WatchedAt { get; set; }
    }

    /// <summary>API'deki <c>EpisodePageDto</c> ile aynı şekilde — bölüm sayfası tek istekte alır.</summary>
    public class EpisodePageDto
    {
        public int TmdbShowId { get; set; }
        public string ShowName { get; set; } = string.Empty;

        public int SeasonNumber { get; set; }
        public string? SeasonName { get; set; }

        public int Id { get; set; }
        public int EpisodeNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Overview { get; set; }
        public string? StillPath { get; set; }
        public DateTime? AirDate { get; set; }
        public int? Runtime { get; set; }
        public double TmdbVoteAverage { get; set; }

        public bool Watched { get; set; }
        public decimal? MyRating { get; set; }

        public EpisodeRefDto? Previous { get; set; }
        public EpisodeRefDto? Next { get; set; }
    }

    public class EpisodeRefDto
    {
        public int SeasonNumber { get; set; }
        public int EpisodeNumber { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class MarkWatchedRequest
    {
        public bool Watched { get; set; } = true;
    }

    public class ShowProgressDto
    {
        public int TmdbId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int TotalEpisodes { get; set; }
        public int WatchedEpisodes { get; set; }
        public NextEpisodeDto? NextEpisode { get; set; }
    }

    public class NextEpisodeDto
    {
        public int TmdbShowId { get; set; }
        public string ShowName { get; set; } = string.Empty;
        public string? ShowPosterPath { get; set; }
        public int SeasonNumber { get; set; }
        public int EpisodeNumber { get; set; }
        public string EpisodeName { get; set; } = string.Empty;
        public DateTime? AirDate { get; set; }
        public bool IsUnaired { get; set; }
    }

    public class UpcomingEpisodeDto
    {
        public int TmdbShowId { get; set; }
        public string ShowName { get; set; } = string.Empty;
        public string? ShowPosterPath { get; set; }
        public int SeasonNumber { get; set; }
        public int EpisodeNumber { get; set; }
        public string EpisodeName { get; set; } = string.Empty;
        public DateTime AirDate { get; set; }
    }
}
