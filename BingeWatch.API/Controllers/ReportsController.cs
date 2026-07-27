using System.Security.Claims;
using BingeWatch.API.Configurations;
using BingeWatch.API.Dtos;
using BingeWatch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BingeWatch.API.Controllers
{
    [ApiController]
    [Route("api/reports")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        /// <summary>
        /// İçerik bildirimi. Bildirim spam'ı moderasyon kuyruğunu işe yaramaz hale
        /// getirdiği için burada ayrı ve dar bir kota var.
        /// </summary>
        [HttpPost]
        [EnableRateLimiting(RateLimitPolicies.Report)]
        public async Task<IActionResult> Create([FromBody] CreateReportRequest request)
        {
            var result = await _reportService.CreateAsync(CurrentUserId, request);

            return result switch
            {
                ReportResult.TargetNotFound => NotFound(),
                ReportResult.Self => BadRequest(new { message = "Kendi içeriğini bildiremezsin." }),
                ReportResult.AlreadyReported => Conflict(new { message = "Bu içeriği zaten bildirdin." }),
                _ => NoContent()
            };
        }
    }
}
