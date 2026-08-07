namespace BingeWatch.Web.Dtos
{
    public class GameRoundDto
    {
        public GameContenderDto Left { get; set; } = new();
        public GameContenderDto Right { get; set; } = new();
        public int WinnerTmdbId { get; set; }
        public bool IsTie { get; set; }
    }

    public class GameContenderDto
    {
        public int TmdbId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
        public int? FirstAirYear { get; set; }
        public double VoteAverage { get; set; }
    }

    public class TriviaQuestionDto
    {
        public string Question { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
        public List<string> Options { get; set; } = new();
        public int CorrectIndex { get; set; }
        public string? Explanation { get; set; }
    }
}
