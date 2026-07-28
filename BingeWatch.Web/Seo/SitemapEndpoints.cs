using System.Text;
using System.Xml;

namespace BingeWatch.Web.Seo
{
    /// <summary>
    /// <c>/robots.txt</c> ve <c>/sitemap.xml</c>. Statik dosya yerine uç nokta
    /// olmalarının sebebi: sitemap katalogdan üretiliyor ve host adı isteğe göre
    /// değişiyor (yerel, staging, üretim aynı dosyayı paylaşamaz).
    /// </summary>
    public static class SitemapEndpoints
    {
        /// <summary>
        /// Taramaya kapalı yollar. Bunlar ya kişisel (profil ayarları, bildirimler)
        /// ya da sonsuz varyasyon üreten filtre sayfaları — tarama bütçesini yiyip
        /// karşılığında hiçbir şey getirmiyorlar.
        /// </summary>
        private static readonly string[] DisallowedPaths =
        [
            "/admin/",
            "/settings/",
            "/notifications",
            "/feed",
            "/watchlist",
            "/account/",
            "/login",
            "/register"
        ];

        public static void MapSeoEndpoints(this WebApplication app)
        {
            app.MapGet("/robots.txt", (HttpContext http) =>
            {
                var baseUrl = BaseUrl(http);
                var sb = new StringBuilder();

                sb.AppendLine("User-agent: *");
                foreach (var path in DisallowedPaths)
                    sb.AppendLine($"Disallow: {path}");

                sb.AppendLine();
                sb.AppendLine($"Sitemap: {baseUrl}/sitemap.xml");

                return Results.Text(sb.ToString(), "text/plain", Encoding.UTF8);
            }).AllowAnonymous();

            app.MapGet("/sitemap.xml", async (HttpContext http, IHttpClientFactory factory,
                ILoggerFactory loggerFactory) =>
            {
                var baseUrl = BaseUrl(http);
                var client = factory.CreateClient("ApiClient");
                var logger = loggerFactory.CreateLogger(nameof(SitemapEndpoints));

                var shows = await TryGetAsync(client, "api/sitemap/shows", logger);
                var lists = await TryGetAsync(client, "api/sitemap/lists", logger);

                var xml = BuildSitemap(baseUrl, shows, lists);
                return Results.Text(xml, "application/xml", Encoding.UTF8);
            }).AllowAnonymous();
        }

        /// <summary>
        /// API'ye ulaşılamazsa sitemap boş değil <b>eksik</b> dönmeli: statik
        /// sayfalar yine listelenir. 500 dönmek arama motoruna "bu site bozuk"
        /// sinyali verir ve sitemap bir süre yeniden denenmez.
        /// </summary>
        private static async Task<List<SitemapEntry>> TryGetAsync(HttpClient client, string path,
            ILogger logger)
        {
            try
            {
                return await client.GetFromJsonAsync<List<SitemapEntry>>(path) ?? new();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Sitemap için {Path} okunamadı; o bölüm atlanıyor.", path);
                return new();
            }
        }

        private static string BuildSitemap(string baseUrl, List<SitemapEntry> shows,
            List<SitemapEntry> lists)
        {
            // StringBuilder'a yazınca XmlWriter bildirime encoding="utf-16" koyuyor
            // (StringBuilder'ın kendi kodlaması), yanıt ise UTF-8 gidiyor. Bu
            // uyuşmazlık bazı ayrıştırıcıları düşürüyor; kodlamayı UTF-8 bildiren
            // bir StringWriter kullanılıyor.
            var sb = new StringBuilder();
            using var stringWriter = new Utf8StringWriter(sb);
            using var writer = XmlWriter.Create(stringWriter, new XmlWriterSettings
            {
                Indent = true,
                Encoding = Encoding.UTF8,
                OmitXmlDeclaration = false
            });

            writer.WriteStartDocument();
            writer.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

            // Statik giriş noktaları. Ana sayfa [Authorize] olduğu için buraya
            // girmiyor; anonim ziyaretçinin karşılaştığı ilk sayfa /series.
            WriteUrl(writer, $"{baseUrl}/series", null, "daily", "1.0");
            WriteUrl(writer, $"{baseUrl}/discover", null, "daily", "0.8");
            WriteUrl(writer, $"{baseUrl}/reviews", null, "daily", "0.7");
            WriteUrl(writer, $"{baseUrl}/lists", null, "daily", "0.6");
            WriteUrl(writer, $"{baseUrl}/search", null, "monthly", "0.3");

            foreach (var show in shows)
                WriteUrl(writer, $"{baseUrl}/show/{show.TmdbId}", show.LastModified, "weekly", "0.9");

            foreach (var list in lists)
                WriteUrl(writer, $"{baseUrl}/list/{list.TmdbId}", list.LastModified, "weekly", "0.5");

            writer.WriteEndElement();
            writer.WriteEndDocument();
            writer.Flush();

            return sb.ToString();
        }

        private static void WriteUrl(XmlWriter writer, string location, DateTime? lastModified,
            string changeFrequency, string priority)
        {
            writer.WriteStartElement("url");
            writer.WriteElementString("loc", location);

            if (lastModified.HasValue)
                writer.WriteElementString("lastmod", lastModified.Value.ToString("yyyy-MM-dd"));

            writer.WriteElementString("changefreq", changeFrequency);
            writer.WriteElementString("priority", priority);
            writer.WriteEndElement();
        }

        /// <summary>
        /// İstekteki şema ve host. Ters vekil arkasında doğru olması
        /// <c>UseForwardedHeaders</c>'a bağlı (bkz. Program.cs) — yoksa sitemap
        /// http:// adresler yayınlar.
        /// </summary>
        private static string BaseUrl(HttpContext http) =>
            $"{http.Request.Scheme}://{http.Request.Host}";

        private sealed class SitemapEntry
        {
            public int TmdbId { get; set; }
            public DateTime LastModified { get; set; }
        }

        /// <summary>
        /// XML bildirimine <c>encoding="utf-8"</c> yazdırmak için; varsayılan
        /// <see cref="StringWriter"/> UTF-16 bildiriyor.
        /// </summary>
        private sealed class Utf8StringWriter : StringWriter
        {
            public Utf8StringWriter(StringBuilder sb) : base(sb) { }

            public override Encoding Encoding => Encoding.UTF8;
        }
    }
}
