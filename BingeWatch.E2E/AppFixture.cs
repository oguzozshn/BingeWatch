using System.Diagnostics;
using Microsoft.Playwright;

namespace BingeWatch.E2E
{
    /// <summary>
    /// Tüm E2E testleri için ortak ortam: API ve Web süreçlerini ayağa kaldırır,
    /// Playwright tarayıcısını açar. Koleksiyon başına tek örnek — her test için
    /// uygulama başlatmak dakikalar sürerdi.
    /// </summary>
    public class AppFixture : IAsyncLifetime
    {
        /// <summary>
        /// Geliştirme sırasında elle çalıştırılan sunucularla (5054/5162)
        /// çakışmasın diye ayrı portlar.
        /// </summary>
        public const string ApiUrl = "http://localhost:5074";
        public const string WebUrl = "http://localhost:5182";

        /// <summary>
        /// Testler geliştirme veritabanını kirletmesin diye ayrı bir veritabanı.
        /// Kayıt/puanlama testleri satır yazıyor; bunlar `BingeOnDb`'ye karışırsa
        /// elle bakılan veriyle test verisi ayırt edilemez hale gelir.
        /// </summary>
        private const string ConnectionString =
            "Server=(localdb)\\mssqllocaldb;Database=BingeWatchDb_E2E;" +
            "Trusted_Connection=true;MultipleActiveResultSets=true";

        private Process? _api;
        private Process? _web;

        // Sunucu çıktısı tamponlanıyor: ayağa kalkmazsa sebebini görebilelim.
        // Boruyu okumazsak süreç dolduğunda ayrıca kilitleniyor.
        private readonly List<string> _apiOutput = new();
        private readonly List<string> _webOutput = new();

        public IPlaywright Playwright { get; private set; } = null!;
        public IBrowser Browser { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            var root = FindSolutionRoot();

            // API açılışta migration uyguluyor; veritabanını o oluşturur.
            _api = StartServer(root, "BingeWatch.API", _apiOutput, ApiUrl,
                ("ConnectionStrings__DefaultConnection", ConnectionString));
            _web = StartServer(root, "BingeWatch.Web", _webOutput, WebUrl, ("Api__BaseUrl", ApiUrl + "/"));

            await WaitForHealthAsync(ApiUrl + "/health", "API", _apiOutput);
            await WaitForHealthAsync(WebUrl + "/health", "Web", _webOutput);

            // Şema hazır olduktan sonra tohumla: katalog testlerin kontrolünde
            // olsun, TMDb'ye gidilmesin.
            await CatalogSeeder.SeedAsync(ConnectionString);

            Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            // Tarayıcıdan sonra: kayıt formu üzerinden açılıyorlar.
            PrimaryUser = await RegisterAsync("gezgin");
            SecondaryUser = await RegisterAsync("komsu");
        }

        public async Task DisposeAsync()
        {
            if (Browser != null)
                await Browser.CloseAsync();

            Playwright?.Dispose();

            Stop(_api);
            Stop(_web);
        }

        /// <summary>
        /// Web sunucusunun son log satırları. Tarayıcı tarafı "unhandled
        /// exception on the current circuit" dediğinde asıl yığın izi burada;
        /// onsuz hata mesajı teşhis için işe yaramıyor.
        /// </summary>
        public string WebLogTail(int lines = 40)
        {
            lock (_webOutput)
                return string.Join(Environment.NewLine, _webOutput.TakeLast(lines));
        }

        /// <summary>Her test kendi tarayıcı bağlamında koşar; çerezler sızmasın.</summary>
        public async Task<IPage> NewPageAsync(TestUser? user = null)
        {
            var context = await Browser.NewContextAsync(new BrowserNewContextOptions
            {
                Locale = "tr-TR",
                ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
                // Oturum çerezi bağlama enjekte ediliyor; her test yeniden giriş
                // yapsaydı giriş uçlarının IP başına 10/5dk kotası dolardı.
                StorageState = user?.StorageState
            });

            return await context.NewPageAsync();
        }

        /// <summary>Girişli testler için hesap; koleksiyon başına bir kez açılır.</summary>
        public TestUser PrimaryUser { get; private set; } = null!;

        /// <summary>Engelleme gibi iki taraflı akışlar için ikinci hesap.</summary>
        public TestUser SecondaryUser { get; private set; } = null!;

        /// <summary>
        /// Identity varsayılanları büyük/küçük harf, rakam ve sembol istiyor;
        /// yalnızca uzunluk (6) gevşetilmiş.
        /// </summary>
        private const string Password = "Test!1234";

        /// <summary>
        /// Kayıt formunu gerçekten doldurarak hesap açar — API'ye doğrudan POST
        /// atmak daha hızlı olurdu ama kayıt akışının kendisi de test edilen
        /// yüzeyin parçası.
        /// </summary>
        private async Task<TestUser> RegisterAsync(string prefix)
        {
            // Veritabanı çalıştırmalar arasında kalıcı; sabit kullanıcı adı
            // ikinci koşuda "zaten var" hatası verirdi.
            var username = prefix + Guid.NewGuid().ToString("N")[..8];

            var context = await Browser.NewContextAsync(new BrowserNewContextOptions
            {
                Locale = "tr-TR"
            });

            try
            {
                var page = await context.NewPageAsync();
                await page.GotoAsync($"{WebUrl}/register");

                await page.FillAsync("#register-username", username);
                await page.FillAsync("#register-displayname", $"Test {prefix}");
                await page.FillAsync("#register-email", $"{username}@ornek.test");
                await page.FillAsync("#register-password", Password);
                await page.ClickAsync("form[action='/account/register'] button[type=submit]");

                // Başarılı kayıt ana sayfaya döner; hata /register'a geri atar.
                await page.WaitForURLAsync(url => !url.Contains("/register"),
                    new PageWaitForURLOptions { Timeout = 20_000 });

                var state = await context.StorageStateAsync();
                return new TestUser(username, state);
            }
            finally
            {
                await context.CloseAsync();
            }
        }

        private static Process StartServer(string root, string project, List<string> output,
            string url, params (string Key, string Value)[] environment)
        {
            var info = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            info.ArgumentList.Add("run");
            info.ArgumentList.Add("--project");
            info.ArgumentList.Add(Path.Combine(root, project, project + ".csproj"));
            info.ArgumentList.Add("--urls");
            info.ArgumentList.Add(url);

            // Testler Development'ta koşuyor: Production'da HTTPS yönlendirmesi
            // ve HSTS devreye girip düz HTTP isteklerini kırardı.
            info.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            foreach (var (key, value) in environment)
                info.Environment[key] = value;

            var process = Process.Start(info)
                ?? throw new InvalidOperationException($"{project} başlatılamadı.");

            void Capture(object _, DataReceivedEventArgs e)
            {
                if (e.Data == null)
                    return;

                lock (output)
                {
                    // Son satırlar yeter; sunucu saatlerce koşarsa bellek şişmesin.
                    output.Add(e.Data);
                    if (output.Count > 200)
                        output.RemoveRange(0, output.Count - 200);
                }
            }

            process.OutputDataReceived += Capture;
            process.ErrorDataReceived += Capture;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return process;
        }

        /// <summary>
        /// Sunucu hazır olana kadar bekler. İlk açılışta derleme + migration
        /// var, o yüzden pencere geniş tutuldu.
        /// </summary>
        private static async Task WaitForHealthAsync(string healthUrl, string name, List<string> output)
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var deadline = DateTime.UtcNow.AddMinutes(3);

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var response = await client.GetAsync(healthUrl);
                    if (response.IsSuccessStatusCode)
                        return;
                }
                catch (HttpRequestException)
                {
                    // Henüz dinlemiyor.
                }
                catch (TaskCanceledException)
                {
                    // Zaman aşımı; tekrar dene.
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            string tail;
            lock (output)
                tail = string.Join(Environment.NewLine, output.TakeLast(40));

            throw new TimeoutException(
                $"{name} ({healthUrl}) 3 dakikada hazır olmadı. Son çıktı:{Environment.NewLine}{tail}");
        }

        private static void Stop(Process? process)
        {
            if (process == null || process.HasExited)
                return;

            try
            {
                // dotnet run alt süreç doğuruyor; ağacı birlikte öldür.
                process.Kill(entireProcessTree: true);
                process.WaitForExit(10_000);
            }
            catch (InvalidOperationException)
            {
                // Zaten kapanmış.
            }
        }

        private static string FindSolutionRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "BingeWatch.sln")))
                dir = dir.Parent;

            return dir?.FullName
                ?? throw new InvalidOperationException("BingeWatch.sln bulunamadı.");
        }
    }

    [CollectionDefinition(Name)]
    public class AppCollection : ICollectionFixture<AppFixture>
    {
        public const string Name = "app";
    }

    /// <summary>
    /// Test hesabı. <paramref name="StorageState"/> Playwright'ın çerez
    /// anlık görüntüsü — yeni bağlama verildiğinde oturum hazır gelir.
    /// </summary>
    public record TestUser(string Username, string StorageState);
}
