using BingeWatch.API.Dtos;
using BingeWatch.API.Models;

namespace BingeWatch.API.Services
{
    public interface IReportService
    {
        Task<ReportResult> CreateAsync(string reporterId, CreateReportRequest request);

        /// <summary>Moderasyon kuyruğu; <paramref name="status"/> boşsa yalnızca açıklar döner.</summary>
        Task<List<ReportDto>> GetQueueAsync(ReportStatus? status, int skip, int take);

        Task<int> GetOpenCountAsync();

        /// <summary>Bildirimi kapatır; eylem "içeriği sil" ise içerik de kaldırılır.</summary>
        Task<bool> ResolveAsync(string moderatorId, int reportId, ResolveReportRequest request);
    }
}
