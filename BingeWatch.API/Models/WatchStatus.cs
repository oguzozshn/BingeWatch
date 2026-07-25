namespace BingeWatch.API.Models
{
    /// <summary>
    /// Kullanıcının bir diziyle ilişkisi. Sayısal değerler DB'de saklandığı için
    /// mevcut üyelerin değerleri değiştirilmemeli; yeni durumlar sona eklenmeli.
    /// </summary>
    public enum WatchStatus
    {
        PlanToWatch = 0,
        Watching = 1,
        Completed = 2,
        Dropped = 3,
        OnHold = 4
    }
}
