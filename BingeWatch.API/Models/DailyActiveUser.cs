namespace BingeWatch.API.Models
{
    /// <summary>
    /// "O gün uygulamayı kullanan" kaydı — gün başına kullanıcı başına tek satır.
    /// </summary>
    /// <remarks>
    /// <c>AppUser.LastSeenAt</c> yalnızca <i>son</i> görülme anını tutuyor, yani
    /// "şu an kaç kişi çevrimiçi" sorusuna cevap veriyor ama "dün kaç kişi girdi"
    /// sorusuna veremiyor: bugün giren bir kullanıcının dünkü izi siliniyor.
    /// Günlük tekil sayım bu yüzden ayrı tutuluyor.
    ///
    /// <c>ActivityEvents</c> de bu işi göremezdi: orası sosyal eylemleri (beğeni,
    /// yorum, takip) kaydediyor, sadece gezinen kullanıcıyı görmüyor.
    /// </remarks>
    public class DailyActiveUser
    {
        public int Id { get; set; }

        /// <summary>Gün (UTC).</summary>
        public DateOnly Day { get; set; }

        public string UserId { get; set; } = string.Empty;
        public AppUser? User { get; set; }
    }
}
