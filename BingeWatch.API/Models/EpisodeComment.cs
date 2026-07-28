namespace BingeWatch.API.Models
{
    /// <summary>
    /// Bir bölümün tartışma yorumu. <see cref="Review"/> ile karıştırılmamalı:
    /// inceleme yayılır (akışa düşer, <c>/reviews</c> sayfasında listelenir),
    /// bölüm yorumu yayılmaz — yalnızca bölümün kendi satırında görünür.
    ///
    /// ROADMAP §3 bölüm bazlı *incelemeyi* reddediyor; gerekçe akışın spoiler
    /// çöplüğüne dönmesiydi. Burada olay yazılmadığı için o gerekçe geçerli değil:
    /// kullanıcı yorumları görmeye kendi gidiyor, yorum kimsenin akışına düşmüyor.
    ///
    /// Spoiler koruması bayrağa değil veriye dayanıyor: ipliği yalnızca bölümü
    /// izlemiş olan okuyabiliyor (bkz. <c>EpisodeCommentService</c>).
    /// </summary>
    public class EpisodeComment
    {
        public int Id { get; set; }

        /// <summary>Yerel katalog bölüm id'si (TMDb id'si değil).</summary>
        public int EpisodeId { get; set; }
        public Episode? Episode { get; set; }

        public string UserId { get; set; } = string.Empty;
        public AppUser? User { get; set; }

        public string Body { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
