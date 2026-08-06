using System.Security.Claims;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BingeWatch.API.Controllers
{
    /// <summary>
    /// Moderasyon ve işletim paneli. Rol JWT'nin içinde taşınır (bkz. TokenService);
    /// rolü olmayan bir token bu uçlara hiç giremez.
    /// </summary>
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = AppRoles.Admin)]
    public class AdminController : ControllerBase
    {
        private readonly IReportService _reportService;
        private readonly IAdminStatsService _statsService;
        private readonly ILogFileReader _logReader;

        public AdminController(
            IReportService reportService,
            IAdminStatsService statsService,
            ILogFileReader logReader)
        {
            _reportService = reportService;
            _statsService = statsService;
            _logReader = logReader;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        /// <summary>Moderasyon kuyruğu; <c>status</c> verilmezse yalnızca açık bildirimler.</summary>
        [HttpGet("reports")]
        public async Task<IActionResult> GetReports([FromQuery] ReportStatus? status = null,
            [FromQuery] string? cursor = null, [FromQuery] int take = 25) =>
            Ok(await _reportService.GetQueueAsync(status, cursor, take));

        [HttpGet("reports/open-count")]
        public async Task<IActionResult> GetOpenCount() =>
            Ok(new { count = await _reportService.GetOpenCountAsync() });

        [HttpPost("reports/{reportId:int}/resolve")]
        public async Task<IActionResult> Resolve(int reportId, [FromBody] ResolveReportRequest request)
        {
            var resolved = await _reportService.ResolveAsync(CurrentUserId, reportId, request);
            return resolved ? NoContent() : NotFound();
        }

        /// <summary>Panelin üst kısmındaki sayaçlar: kullanıcı, trafik, içerik.</summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats(CancellationToken cancellationToken) =>
            Ok(await _statsService.GetAsync(cancellationToken));

        /// <summary>
        /// Uygulama logunun son satırları, eskiden yeniye.
        /// </summary>
        /// <remarks>
        /// Log satırları ham metin olarak dönüyor; panel bunları biçimlendirmeden
        /// basıyor. Loga kullanıcı verisi düşebildiği için uç yalnızca Admin
        /// rolüne açık — sınıf üzerindeki <c>[Authorize]</c> bunu zaten sağlıyor.
        /// </remarks>
        [HttpGet("logs")]
        public IActionResult GetLogs([FromQuery] int lines = 200) =>
            Ok(new { lines = _logReader.ReadTail(lines) });
    }
}
