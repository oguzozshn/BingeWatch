namespace BingeWatch.API.Models
{
    /// <summary>
    /// Bir güne ait istek ve trafik toplamı. Her istek için satır yazmak yerine
    /// bellekte biriktirilip periyodik olarak bu tabloya ekleniyor
    /// (bkz. <c>RequestMetricsCollector</c>, <c>MetricsFlushService</c>) — istek
    /// başına bir INSERT, ölçmek istediğimiz yükü kendisi üretirdi.
    /// </summary>
    public class DailyTrafficStat
    {
        public int Id { get; set; }

        /// <summary>Gün (UTC). Sunucu ile panel aynı takvimi konuşsun diye tarih olarak tutuluyor.</summary>
        public DateOnly Day { get; set; }

        /// <summary>O gün API'ye gelen istek sayısı.</summary>
        public long Requests { get; set; }

        /// <summary>O gün istemcilere yazılan yanıt gövdesi toplamı (bayt).</summary>
        public long ResponseBytes { get; set; }
    }
}
