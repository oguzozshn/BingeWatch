namespace BingeWatch.API.Dtos
{
    public class UpsertListRequest
    {
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>Kapalıysa liste yalnızca sahibine görünür.</summary>
        public bool IsPublic { get; set; } = true;
    }

    public class AddListItemRequest
    {
        /// <summary>TMDb dizi kimliği; katalogda yoksa çekilir.</summary>
        public int TmdbShowId { get; set; }

        public string? Note { get; set; }
    }

    public class UpdateListItemRequest
    {
        public string? Note { get; set; }
    }

    public class ReorderListRequest
    {
        /// <summary>Öğe id'leri istenen sırada. Listeye ait olmayan id'ler yoksayılır.</summary>
        public List<int> ItemIds { get; set; } = new();
    }

    public class UserListSummaryDto
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsPublic { get; set; }

        public string OwnerUsername { get; set; } = string.Empty;
        public string OwnerDisplayName { get; set; } = string.Empty;
        public string? OwnerAvatarUrl { get; set; }

        public int ItemCount { get; set; }

        /// <summary>Kart önizlemesi için ilk sıradaki en fazla 4 posterin yolu.</summary>
        public List<string> PreviewPosterPaths { get; set; } = new();

        /// <summary>İsteği yapan listenin sahibi mi? Düzenleme kontrolleri buna bakar.</summary>
        public bool IsOwner { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UserListDetailDto : UserListSummaryDto
    {
        public List<UserListItemDto> Items { get; set; } = new();
    }

    public class UserListItemDto
    {
        public int Id { get; set; }

        public int TmdbShowId { get; set; }
        public string ShowName { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
        public int? FirstAirYear { get; set; }

        public int Position { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>
    /// "Listeye ekle" menüsünün tek isteği: kullanıcının listeleri ve o dizinin
    /// hangilerinde olduğu. Menü başına N istek atılmasın diye tek uçta birleşti.
    /// </summary>
    public class ListMembershipDto
    {
        public int ListId { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsPublic { get; set; }
        public bool ContainsShow { get; set; }
    }
}
