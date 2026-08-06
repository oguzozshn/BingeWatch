using System.Security.Claims;

namespace BingeWatch.API.Services
{
    /// <summary>
    /// Her isteği sayar ve yanıt gövdesinin boyutunu ölçer; sonucu
    /// <see cref="RequestMetricsCollector"/>'a bırakır.
    /// </summary>
    /// <remarks>
    /// Kimlik doğrulamadan <i>sonra</i> yerleştirilmeli — kullanıcı kimliği
    /// olmadan "bugün kaç kişi girdi" sayılamaz.
    /// </remarks>
    public class RequestMetricsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly RequestMetricsCollector _collector;

        public RequestMetricsMiddleware(RequestDelegate next, RequestMetricsCollector collector)
        {
            _next = next;
            _collector = collector;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Health yoklamaları sayaca girmemeli: Docker bunları 15 saniyede bir
            // çağırıyor, günde ~5.700 istek eder ve gerçek kullanımı gölgede
            // bırakırdı. Panelde "uygulama trafiği" görmek istiyoruz, konteyner
            // yoklamasını değil.
            if (IsHealthProbe(context.Request.Path))
            {
                await _next(context);
                return;
            }

            var originalBody = context.Response.Body;
            var counting = new CountingStream(originalBody);
            context.Response.Body = counting;

            try
            {
                await _next(context);
            }
            finally
            {
                // Gövde akışı her durumda geri konmalı; aksi halde sonraki
                // middleware'ler sarmalanmış akışla çalışmaya devam eder.
                context.Response.Body = originalBody;

                var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                _collector.Record(counting.BytesWritten, userId);
            }
        }

        private static bool IsHealthProbe(PathString path) =>
            path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Yazılan baytları sayıp akışı olduğu gibi ileten sarmalayıcı.
        /// </summary>
        /// <remarks>
        /// <c>Response.ContentLength</c> yeterli olmuyor: parçalı (chunked) yanıtlarda
        /// ve akış hâlinde üretilen içerikte <c>null</c> kalıyor. Sayının doğru olması
        /// için gerçekten yazılan baytı saymak gerekiyor.
        /// </remarks>
        private sealed class CountingStream : Stream
        {
            private readonly Stream _inner;

            public CountingStream(Stream inner) => _inner = inner;

            public long BytesWritten { get; private set; }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                BytesWritten += count;
                _inner.Write(buffer, offset, count);
            }

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                BytesWritten += buffer.Length;
                _inner.Write(buffer);
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                BytesWritten += count;
                return _inner.WriteAsync(buffer, offset, count, cancellationToken);
            }

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                BytesWritten += buffer.Length;
                return _inner.WriteAsync(buffer, cancellationToken);
            }

            public override void Flush() => _inner.Flush();
            public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }
}
