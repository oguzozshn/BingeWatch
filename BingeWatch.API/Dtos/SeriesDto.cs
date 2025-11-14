namespace BingeWatch.API.Dtos
{
    public class SeriesDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Overview { get; set; }
        public string PosterPath { get; set; }
        public DateTime? FirstAirDate { get; set; }
    }
}
