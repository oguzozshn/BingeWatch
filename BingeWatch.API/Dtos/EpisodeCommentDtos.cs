namespace BingeWatch.API.Dtos
{
    /// <summary>
    /// Bölümün yorum ipliği. İplik kilitliyse <see cref="Comments"/> boş döner —
    /// yorum sayısı bile verilmez: "burada 40 yorum var" bilgisi tek başına
    /// bölüm hakkında bir şey söyler (tartışılan bir olay olmuş).
    /// </summary>
    public class EpisodeCommentThreadDto
    {
        public int EpisodeId { get; set; }

        /// <summary>İsteği yapan bölümü izlemediyse (ya da anonimse) <c>true</c>.</summary>
        public bool Locked { get; set; }

        /// <summary>Bölüm henüz yayınlanmadıysa <c>true</c>; kilidin sebebi farklı anlatılır.</summary>
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

        /// <summary>
        /// İsteği yapan bu yorumu silebilir mi? İnceleme yorumundan farklı olarak
        /// yalnızca yorumun sahibi siler: ipliğin sahibi yok, bölüm herkesin.
        /// </summary>
        public bool CanDelete { get; set; }
    }

    public class AddEpisodeCommentRequest
    {
        public string Body { get; set; } = string.Empty;
    }
}
