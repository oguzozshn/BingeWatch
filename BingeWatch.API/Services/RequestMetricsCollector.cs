using System.Collections.Concurrent;

namespace BingeWatch.API.Services
{
    /// <summary>Bir yazma aralığında biriken metrikler.</summary>
    public record MetricsSnapshot(long Requests, long ResponseBytes, IReadOnlyCollection<string> UserIds)
    {
        public bool IsEmpty => Requests == 0 && UserIds.Count == 0;
    }

    /// <summary>
    /// İstek sayacı ve trafik toplamı — bellekte. Singleton.
    /// </summary>
    /// <remarks>
    /// Her istek için veritabanına yazmak, ölçmek istediğimiz yükü kendisi
    /// üretirdi: sayfa başına onlarca API çağrısı olan bir uygulamada sayaç,
    /// saydığı trafiğin en pahalı parçası olurdu. Bunun yerine bellekte
    /// birikiyor, <c>MetricsFlushService</c> periyodik olarak boşaltıyor.
    ///
    /// Bunun bedeli: süreç, boşaltma aralığı dolmadan ölürse o aralıktaki
    /// sayaçlar kaybolur. İşletim metriği için kabul edilebilir bir takas —
    /// muhasebe kaydı tutmuyoruz.
    /// </remarks>
    public class RequestMetricsCollector
    {
        private long _requests;
        private long _responseBytes;

        // Aynı kullanıcı aralık içinde defalarca görülebilir; tekilleştirme burada
        // yapılıyor ki veritabanına gereksiz satır gitmesin.
        private readonly ConcurrentDictionary<string, byte> _userIds = new();

        public void Record(long responseBytes, string? userId)
        {
            Interlocked.Increment(ref _requests);

            if (responseBytes > 0)
                Interlocked.Add(ref _responseBytes, responseBytes);

            if (!string.IsNullOrEmpty(userId))
                _userIds.TryAdd(userId, 0);
        }

        /// <summary>
        /// Biriken değerleri alıp sayaçları sıfırlar.
        /// </summary>
        /// <remarks>
        /// Üç alan tek bir kilit altında değil; boşaltma sırasında gelen bir istek
        /// bu aralık yerine bir sonrakine sayılabilir. Toplam korunduğu için
        /// (hiçbir istek düşmüyor, yalnızca komşu aralığa kayıyor) sayaç doğru
        /// kalıyor; kilit almanın maliyeti bu hassasiyete değmez.
        /// </remarks>
        public MetricsSnapshot Drain()
        {
            var requests = Interlocked.Exchange(ref _requests, 0);
            var bytes = Interlocked.Exchange(ref _responseBytes, 0);

            var userIds = _userIds.Keys.ToArray();
            foreach (var userId in userIds)
                _userIds.TryRemove(userId, out _);

            return new MetricsSnapshot(requests, bytes, userIds);
        }
    }
}
