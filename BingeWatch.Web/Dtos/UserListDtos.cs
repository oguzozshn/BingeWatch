namespace BingeWatch.Web.Dtos
{
    public class UpsertListRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsPublic { get; set; } = true;
    }

    public class AddListItemRequest
    {
        public int TmdbShowId { get; set; }
        public string? Note { get; set; }
    }

    public class UpdateListItemRequest
    {
        public string? Note { get; set; }
    }

    public class ReorderListRequest
    {
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
        public List<string> PreviewPosterPaths { get; set; } = new();

        public int LikeCount { get; set; }
        public bool LikedByViewer { get; set; }

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

    public class ListLikeStateDto
    {
        public int ListId { get; set; }
        public int LikeCount { get; set; }
        public bool LikedByViewer { get; set; }
    }

    public enum ListSort
    {
        Recent = 0,
        MostLiked = 1,
        Largest = 2
    }

    public class ListMembershipDto
    {
        public int ListId { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsPublic { get; set; }
        public bool ContainsShow { get; set; }
    }
}
