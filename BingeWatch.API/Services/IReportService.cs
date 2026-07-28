using BingeWatch.API.Dtos;
using BingeWatch.API.Models;

namespace BingeWatch.API.Services
{
    public interface IReportService
    {
        Task<ReportResult> CreateAsync(string reporterId, CreateReportRequest request);

        /// <summary>
        /// Moderasyon kuyruğu; <paramref name="status"/> boşsa yalnızca açıklar döner.
        /// İmleç tabanlı sayfalama.
        /// </summary>
        Task<PagedResult<ReportDto>> GetQueueAsync(ReportStatus? status, string? cursor, int take);

        Task<int> GetOpenCountAsync();

        /// <summary>Bildirimi kapatır; eylem "içeriği sil" ise içerik de kaldırılır.</summary>
        Task<bool> ResolveAsync(string moderatorId, int reportId, ResolveReportRequest request);
    }
}
