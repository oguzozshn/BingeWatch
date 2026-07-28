namespace BingeWatch.Web.Dtos
{
    /// <summary>API'deki <c>EpisodeCommentThreadDto</c> ile aynı şekilde.</summary>
    public class EpisodeCommentThreadDto
    {
        public int EpisodeId { get; set; }

        /// <summary>Bölümü izlemeyene (ve anonime) iplik kapalı; yorumlar boş gelir.</summary>
        public bool Locked { get; set; }

        public bool Unaired { get; set; }

        public List<EpisodeCommentDto> Comments { get; set; } = new();
    }

    public class EpisodeCommentDto
    {
        public int Id { get; set; }
        public int EpisodeId { get; set; }

        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }

        public string Body { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public bool CanDelete { get; set; }
    }

    public class AddEpisodeCommentRequest
    {
        public string Body { get; set; } = string.Empty;
    }
}
