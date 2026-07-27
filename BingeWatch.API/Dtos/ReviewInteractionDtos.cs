namespace BingeWatch.API.Dtos
{
    /// <summary>Beğeni butonunun tıklama sonrası durumu.</summary>
    public class ReviewLikeStateDto
    {
        public int ReviewId { get; set; }
        public int LikeCount { get; set; }
        public bool LikedByViewer { get; set; }
    }

    public class AddCommentRequest
    {
        public string Body { get; set; } = string.Empty;
    }

    public class ReviewCommentDto
    {
        public int Id { get; set; }
        public int ReviewId { get; set; }

        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }

        public string Body { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        /// <summary>İsteği yapan bu yorumu silebilir mi? (yorumun sahibi ya da incelemenin sahibi)</summary>
        public bool CanDelete { get; set; }
    }
}
