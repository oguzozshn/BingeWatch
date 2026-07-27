namespace BingeWatch.API.Models
{
    /// <summary>
    /// Kullanıcının derlediği sıralı dizi listesi ("İzlenmesi gereken 10 polisiye" gibi).
    /// Watchlist'ten farkı: watchlist tek ve kişisel bir kuyruk, liste ise küratörlük —
    /// birden çok olabilir, sıralıdır, öğe başına not alır ve herkese açık olabilir.
    /// </summary>
    public class UserList
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public AppUser? User { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>Kapalıysa yalnızca sahibi görür — keşifte ve profilde listelenmez.</summary>
        public bool IsPublic { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<UserListItem> Items { get; set; } = new();
    }
}
