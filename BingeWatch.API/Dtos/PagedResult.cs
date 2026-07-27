using System.Text;

namespace BingeWatch.API.Dtos
{
    /// <summary>
    /// Sayfalanmış yanıt. İstemci <see cref="NextCursor"/>'ı yorumlamaz, olduğu gibi
    /// bir sonraki isteğe geri verir; <c>null</c> ise liste bitmiştir.
    /// </summary>
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();

        /// <summary>Sonraki sayfanın imleci; son sayfada <c>null</c>.</summary>
        public string? NextCursor { get; set; }

        public static PagedResult<T> Empty() => new();
    }

    /// <summary>
    /// İmleç kodlaması. İki biçim var:
    /// <list type="bullet">
    ///   <item><b>keyset</b> (<c>k</c>): son satırın (tarih, id) çifti. Kaydırırken
    ///   araya yeni satır girse bile sayfa kaymaz ve derin sayfada da hızlı kalır.</item>
    ///   <item><b>offset</b> (<c>o</c>): sıralama anahtarı satırda durmadığında
    ///   (beğeni sayısı, hesaplanan puan) mecburen atlanan satır sayısı.</item>
    /// </list>
    /// İstemciye dışarıdan üretilemesin diye değil, <b>ayrıştırılmasın</b> diye
    /// base64'leniyor: biçim değişince istemci kırılmasın.
    /// </summary>
    public static class Cursor
    {
        public static string EncodeKeyset(DateTime timestamp, int id) =>
            Encode($"k:{timestamp.Ticks}:{id}");

        public static string EncodeOffset(int offset) =>
            Encode($"o:{offset}");

        /// <summary>Keyset imlecini çözer; imleç yoksa ya da bozuksa <c>null</c>.</summary>
        public static (DateTime Timestamp, int Id)? DecodeKeyset(string? cursor)
        {
            var parts = Split(cursor, "k", 3);
            if (parts == null || !long.TryParse(parts[1], out var ticks) || !int.TryParse(parts[2], out var id))
                return null;

            if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
                return null;

            return (new DateTime(ticks, DateTimeKind.Utc), id);
        }

        /// <summary>Offset imlecini çözer; imleç yoksa ya da bozuksa 0.</summary>
        public static int DecodeOffset(string? cursor)
        {
            var parts = Split(cursor, "o", 2);
            if (parts == null || !int.TryParse(parts[1], out var offset))
                return 0;

            return Math.Max(offset, 0);
        }

        private static string Encode(string value) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

        /// <summary>
        /// Bozuk imleç hata değil: istemci eski biçimli bir imleci geri gönderdiğinde
        /// listenin başına dönmesi, 400 almasından iyi.
        /// </summary>
        private static string[]? Split(string? cursor, string expectedKind, int expectedParts)
        {
            if (string.IsNullOrWhiteSpace(cursor))
                return null;

            string decoded;
            try
            {
                decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            }
            catch (FormatException)
            {
                return null;
            }

            var parts = decoded.Split(':');
            return parts.Length == expectedParts && parts[0] == expectedKind ? parts : null;
        }
    }
}
