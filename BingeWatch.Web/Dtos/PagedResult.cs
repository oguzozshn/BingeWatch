namespace BingeWatch.Web.Dtos
{
    /// <summary>
    /// API'nin sayfalanmış yanıtı. <see cref="NextCursor"/> opak: yorumlanmaz,
    /// bir sonraki isteğe olduğu gibi geri verilir. <c>null</c> ise liste bitmiştir.
    /// </summary>
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public string? NextCursor { get; set; }
    }
}
