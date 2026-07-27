namespace BingeWatch.API.Models
{
    /// <summary>
    /// Puanın hangi hiyerarşi seviyesine verildiği. Dizi hiyerarşik olduğu için
    /// (film gibi tek nesne değil) puan üç seviyede tutulur.
    /// </summary>
    public enum RatingTargetType
    {
        Show = 0,
        Season = 1,
        Episode = 2
    }
}
