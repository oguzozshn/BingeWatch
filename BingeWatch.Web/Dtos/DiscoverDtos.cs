namespace BingeWatch.Web.Dtos
{
    /// <summary>API'deki <c>WatchStatus</c>'ın aynası.</summary>
    public enum WatchStatus
    {
        PlanToWatch = 0,
        Watching = 1,
        Completed = 2,
        Dropped = 3,
        OnHold = 4
    }

    public enum DiscoverSort
    {
        Popularity = 0,
        Rating = 1,
        Newest = 2
    }

    public class DiscoverResultDto
    {
        public int Page { get; set; }
        public List<DiscoverShowDto> Results { get; set; } = new();
        public bool HasMore { get; set; }
    }

    public class DiscoverShowDto
    {
        public int TmdbId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
        public int? FirstAirYear { get; set; }
        public double VoteAverage { get; set; }
        public WatchStatus? Status { get; set; }
    }

    public class GenreDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class NetworkDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class SearchResultDto
    {
        public List<DiscoverShowDto> Shows { get; set; } = new();
        public List<PersonDto> People { get; set; } = new();
    }

    public class PersonDto
    {
        public int TmdbId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ProfilePath { get; set; }
        public string? KnownForDepartment { get; set; }
        public List<string> KnownForShows { get; set; } = new();
    }

    public class PersonCreditsDto
    {
        public int TmdbId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ProfilePath { get; set; }
        public List<PersonCreditDto> Credits { get; set; } = new();
    }

    public class PersonCreditDto
    {
        public int TmdbShowId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
        public int? FirstAirYear { get; set; }
        public double VoteAverage { get; set; }
        public string? Role { get; set; }
        public int EpisodeCount { get; set; }
    }
}
