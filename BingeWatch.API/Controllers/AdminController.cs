using System.Security.Claims;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using BingeWatch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BingeWatch.API.Controllers
{
    /// <summary>
    /// Moderasyon paneli. Rol JWT'nin içinde taşınır (bkz. TokenService); rolü
    /// olmayan bir token bu uçlara hiç giremez.
    /// </summary>
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = AppRoles.Admin)]
    public class AdminController : ControllerBase
    {
        private readonly IReportService _reportService;

        public AdminController(IReportService reportService)
        {
            _reportService = reportService;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        /// <summary>Moderasyon kuyruğu; <c>status</c> verilmezse yalnızca açık bildirimler.</summary>
        [HttpGet("reports")]
        public async Task<IActionResult> GetReports([FromQuery] ReportStatus? status = null,
            [FromQuery] int skip = 0, [FromQuery] int take = 25) =>
            Ok(await _reportService.GetQueueAsync(status, skip, take));

        [HttpGet("reports/open-count")]
        public async Task<IActionResult> GetOpenCount() =>
            Ok(new { count = await _reportService.GetOpenCountAsync() });

        [HttpPost("reports/{reportId:int}/resolve")]
        public async Task<IActionResult> Resolve(int reportId, [FromBody] ResolveReportRequest request)
        {
            var resolved = await _reportService.ResolveAsync(CurrentUserId, reportId, request);
            return resolved ? NoContent() : NotFound();
        }
    }
}
