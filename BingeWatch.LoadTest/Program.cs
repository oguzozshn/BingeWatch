using NBomber.Contracts.Stats;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace BingeWatch.LoadTest
{
    /// <summary>
    /// Elle çalıştırılan yük teşhisi. <b>CI'ın parçası değil</b> ve olmamalı:
    /// yük üreticisi ile uygulama aynı makinede koştuğu için mutlak sayılar
    /// (saniyede kaç istek kaldırır) donanıma ve o anki yüke bağlı — eşik koyup
    /// CI'ı kırmızıya düşürmek gürültüden ibaret olur.
    /// <para>
    /// Anlamlı olan taraf <b>göreli</b>: hangi uç diğerlerinden kat kat yavaş,
    /// yük artınca p95 nerede kopuyor, bir değişiklikten sonra aynı uç yavaşladı
    /// mı. Bu yüzden rapor klasörü karşılaştırma için saklanmalı.
    /// </para>
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            var apiUrl = Arg(args, "--api") ?? "http://localhost:5054";
            var webUrl = Arg(args, "--web") ?? "http://localhost:5162";
            var showId = int.TryParse(Arg(args, "--show"), out var parsed) ? parsed : 1396;

            Console.WriteLine($"API : {apiUrl}");
            Console.WriteLine($"Web : {webUrl}");
            Console.WriteLine($"Dizi: {showId}");
            Console.WriteLine();
            Console.WriteLine("UYARI: Genel istek tavanı varsayılan olarak dakikada 120.");
            Console.WriteLine("Ölçülen şey uygulama değil jeton kovası olmasın diye API'yi");
            Console.WriteLine("şu ortam değişkenleriyle başlat:");
            Console.WriteLine("  RateLimiting__GlobalTokenLimit=1000000");
            Console.WriteLine("  RateLimiting__GlobalTokensPerMinute=1000000");
            Console.WriteLine();

            using var httpClient = new HttpClient();

            // Katalog okuması: sezon ve bölümleriyle birlikte tek dizi. Dizi
            // sayfasının en ağır API çağrısı bu.
            var showDetail = Scenario.Create("api_dizi_detay", async context =>
            {
                var request = Http.CreateRequest("GET", $"{apiUrl}/api/shows/{showId}");
                return await Http.Send(httpClient, request);
            })
            .WithoutWarmUp()
            .WithLoadSimulations(Simulation.RampingInject(rate: 50,
                interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)));

            // İmleçli liste keşfi: sıralama alt sorgusu ve poster önizlemesi
            // yüzünden katalog okumasından pahalı olması beklenir.
            var listDiscovery = Scenario.Create("api_liste_kesfi", async context =>
            {
                // Sıralama parametresi bilerek verilmiyor: enum adına
                // (ListSort.Recent) bağlanmak senaryoyu kırılgan yapıyor.
                var request = Http.CreateRequest("GET", $"{apiUrl}/api/lists");
                return await Http.Send(httpClient, request);
            })
            .WithoutWarmUp()
            .WithLoadSimulations(Simulation.RampingInject(rate: 50,
                interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)));

            // Blazor Server'ın anonim dizi sayfasını baştan çizmesi. Buradaki
            // maliyet API çağrısının üstüne render'ı da ekliyor; SEO trafiği
            // tam olarak bu yolu kullanıyor.
            var showPage = Scenario.Create("web_dizi_sayfasi", async context =>
            {
                var request = Http.CreateRequest("GET", $"{webUrl}/show/{showId}");
                return await Http.Send(httpClient, request);
            })
            .WithoutWarmUp()
            .WithLoadSimulations(Simulation.RampingInject(rate: 20,
                interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)));

            NBomberRunner
                .RegisterScenarios(showDetail, listDiscovery, showPage)
                .WithReportFolder("load-reports")
                .WithReportFormats(ReportFormat.Html, ReportFormat.Txt)
                .Run();

            return 0;
        }

        private static string? Arg(string[] args, string name)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
