namespace BingeWatch.API.Models
{
    /// <summary>
    /// Listeye verilen beğeni. Keşifte "en beğenilen" sıralamasını besler ve
    /// liste sahibine bildirim üretir.
    /// </summary>
    public class UserListLike
    {
        public int Id { get; set; }

        public int UserListId { get; set; }
        public UserList? UserList { get; set; }

        public string UserId { get; set; } = string.Empty;
        public AppUser? User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
