using System.Text;

namespace BingeWatch.API.Services
{
    public interface ILogFileReader
    {
        /// <summary>En yeni log dosyasının son <paramref name="lines"/> satırı, eskiden yeniye.</summary>
        IReadOnlyList<string> ReadTail(int lines);
    }

    /// <summary>
    /// Admin panelindeki log görüntüleyicinin kaynağı: Serilog'un yazdığı dosyanın
    /// sonundan geriye doğru okur.
    /// </summary>
    /// <remarks>
    /// Konteynerde loglar <c>stdout</c>'a da gidiyor (<c>docker compose logs</c>) —
    /// dosya sink'i onun yerine değil, yanına eklendi. Sebep basit: <c>stdout</c>
    /// uygulamanın kendisi tarafından okunamaz, panelde göstermek için diskte bir
    /// kopya gerekiyor.
    /// </remarks>
    public class LogFileReader : ILogFileReader
    {
        /// <summary>Sondan geriye okurken kullanılan tampon.</summary>
        private const int ChunkSize = 32 * 1024;

        /// <summary>Tek seferde döndürülebilecek en fazla satır.</summary>
        public const int MaxLines = 1000;

        private readonly string _directory;
        private readonly ILogger<LogFileReader> _logger;

        public LogFileReader(IConfiguration configuration, ILogger<LogFileReader> logger)
        {
            _directory = ResolveDirectory(configuration);
            _logger = logger;
        }

        /// <summary>
        /// Log klasörü. Serilog'un yazdığı yerle aynı olmalı; ikisi de aynı
        /// yapılandırma anahtarını okuyor (bkz. Program.cs).
        /// </summary>
        public static string ResolveDirectory(IConfiguration configuration) =>
            configuration.GetValue<string>("Logging:Directory") ?? "logs";

        public IReadOnlyList<string> ReadTail(int lines)
        {
            lines = Math.Clamp(lines, 1, MaxLines);

            try
            {
                var file = NewestLogFile();
                if (file == null)
                    return Array.Empty<string>();

                return ReadLastLines(file, lines);
            }
            catch (Exception ex)
            {
                // Log okunamaması panelin tamamını düşürmemeli; diğer kartlar çalışsın.
                _logger.LogWarning(ex, "Log dosyası okunamadı ({Directory})", _directory);
                return Array.Empty<string>();
            }
        }

        private string? NewestLogFile()
        {
            if (!Directory.Exists(_directory))
                return null;

            return new DirectoryInfo(_directory)
                .GetFiles("*.log")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => f.FullName)
                .FirstOrDefault();
        }

        /// <summary>
        /// Dosyanın sonundan geriye doğru, istenen satır sayısı toplanana kadar okur.
        /// </summary>
        /// <remarks>
        /// Dosyanın tamamını okuyup son N satırı almak daha kısa olurdu ama log
        /// dosyası 10MB'a kadar büyüyebiliyor; 200 satır için 10MB okumak panelin
        /// her yenilenişinde boşa iş demek.
        ///
        /// <c>FileShare.ReadWrite</c> şart: Serilog dosyayı açık tutuyor, paylaşımlı
        /// açmazsak okuma <c>IOException</c> ile düşer.
        /// </remarks>
        private static IReadOnlyList<string> ReadLastLines(string path, int lines)
        {
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            var buffer = new byte[ChunkSize];
            var tail = new List<byte>();
            var newlineCount = 0;
            var position = stream.Length;

            // Satır sonu sayısı hedefi geçene kadar sondan geriye doğru parça parça oku.
            while (position > 0 && newlineCount <= lines)
            {
                var readSize = (int)Math.Min(ChunkSize, position);
                position -= readSize;

                stream.Seek(position, SeekOrigin.Begin);
                var read = stream.Read(buffer, 0, readSize);

                for (var i = read - 1; i >= 0; i--)
                {
                    if (buffer[i] == (byte)'\n')
                        newlineCount++;
                }

                tail.InsertRange(0, buffer.AsSpan(0, read).ToArray());
            }

            var text = Encoding.UTF8.GetString(tail.ToArray());

            return text
                .Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .TakeLast(lines)
                .ToList();
        }
    }
}
