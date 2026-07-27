using BingeWatch.API.Data;
using BingeWatch.API.Dtos;
using BingeWatch.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BingeWatch.API.Services
{
    /// <summary>
    /// İçerik bildirimi ve moderasyon kuyruğu. Bildirim, hedefin sahibini
    /// (<see cref="Report.TargetUserId"/>) kaydeder; içerik kaldırıldıktan sonra
    /// da "bu kullanıcı hakkında kaç bildirim var" sorusu cevaplanabilsin diye.
    /// </summary>
    public class ReportService : IReportService
    {
        private const int MaxNoteLength = 1000;

        /// <summary>Kuyrukta gösterilen içerik parçasının uzunluğu.</summary>
        private const int ExcerptLength = 300;

        private readonly BingeOnDbContext _context;
        private readonly IActivityService _activityService;
        private readonly INotificationService _notificationService;

        public ReportService(BingeOnDbContext context, IActivityService activityService,
            INotificationService notificationService)
        {
            _context = context;
            _activityService = activityService;
            _notificationService = notificationService;
        }

        public async Task<ReportResult> CreateAsync(string reporterId, CreateReportRequest request)
        {
            var ownerId = await ResolveTargetOwnerAsync(request);
            if (ownerId == null)
                return ReportResult.TargetNotFound;

            if (ownerId == reporterId)
                return ReportResult.Self;

            // Aynı hedefi tekrar bildirmek kuyruğu şişirir; ilk bildirim kapanana kadar yeter.
            var duplicate = await _context.Reports.AnyAsync(r =>
                r.ReporterId == reporterId && r.Status == ReportStatus.Open
                && r.TargetType == request.TargetType && r.TargetId == request.TargetId
                && r.TargetUserId == ownerId);
            if (duplicate)
                return ReportResult.AlreadyReported;

            _context.Reports.Add(new Report
            {
                ReporterId = reporterId,
                TargetType = request.TargetType,
                TargetId = request.TargetType == ReportTargetType.User ? null : request.TargetId,
                TargetUserId = ownerId,
                Reason = request.Reason,
                Note = Clip(request.Note)
            });

            await _context.SaveChangesAsync();
            return ReportResult.Ok;
        }

        public async Task<PagedResult<ReportDto>> GetQueueAsync(ReportStatus? status, string? cursor, int take)
        {
            take = Math.Clamp(take, 1, 100);

            var query = _context.Reports
                .Where(r => status == null ? r.Status == ReportStatus.Open : r.Status == status);

            var after = Cursor.DecodeKeyset(cursor);
            if (after != null)
            {
                query = query.Where(r => r.CreatedAt < after.Value.Timestamp
                                      || (r.CreatedAt == after.Value.Timestamp && r.Id < after.Value.Id));
            }

            var reports = await query
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id)
                .Take(take)
                .Include(r => r.Reporter)
                .Include(r => r.TargetUser)
                .Include(r => r.ResolvedBy)
                .ToListAsync();

            if (reports.Count == 0)
                return PagedResult<ReportDto>.Empty();

            // Hedef kullanıcı başına açık bildirim sayısı; moderatör tekrar edeni ayırsın.
            var targetUserIds = reports.Select(r => r.TargetUserId).Distinct().ToList();
            var openCounts = await _context.Reports
                .Where(r => r.Status == ReportStatus.Open && targetUserIds.Contains(r.TargetUserId))
                .GroupBy(r => r.TargetUserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            var previews = await LoadPreviewsAsync(reports);

            var items = reports.Select(r =>
            {
                previews.TryGetValue((r.TargetType, r.TargetId ?? 0), out var preview);
                openCounts.TryGetValue(r.TargetUserId, out var openForTarget);

                return new ReportDto
                {
                    Id = r.Id,
                    TargetType = r.TargetType,
                    TargetId = r.TargetId,
                    Reason = r.Reason,
                    Note = r.Note,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt,
                    ReporterUsername = r.Reporter?.UserName ?? string.Empty,
                    TargetUsername = r.TargetUser?.UserName ?? string.Empty,
                    ContentExcerpt = preview.Excerpt,
                    ContentUrl = preview.Url,
                    // Bu bildirimin kendisi sayıdan düşülür; "başka kaç tane var" sorulur.
                    OtherOpenReportsForTarget = Math.Max(0, openForTarget - (r.Status == ReportStatus.Open ? 1 : 0)),
                    ResolvedAt = r.ResolvedAt,
                    ResolvedByUsername = r.ResolvedBy?.UserName,
                    ResolutionNote = r.ResolutionNote
                };
            }).ToList();

            var last = reports[^1];

            return new PagedResult<ReportDto>
            {
                Items = items,
                NextCursor = reports.Count < take ? null : Cursor.EncodeKeyset(last.CreatedAt, last.Id)
            };
        }

        public Task<int> GetOpenCountAsync() =>
            _context.Reports.CountAsync(r => r.Status == ReportStatus.Open);

        public async Task<bool> ResolveAsync(string moderatorId, int reportId, ResolveReportRequest request)
        {
            var report = await _context.Reports.FirstOrDefaultAsync(r => r.Id == reportId);
            if (report == null || report.Status != ReportStatus.Open)
                return false;

            if (request.Action == ReportAction.DeleteContent)
                await DeleteContentAsync(report);

            report.Status = request.Action == ReportAction.DeleteContent
                ? ReportStatus.Resolved
                : ReportStatus.Dismissed;
            report.ResolvedById = moderatorId;
            report.ResolvedAt = DateTime.UtcNow;
            report.ResolutionNote = Clip(request.Note);

            await _context.SaveChangesAsync();

            // Aynı içerik için bekleyen diğer bildirimler de aynı kararla kapanır;
            // moderatör silinmiş içeriği tekrar tekrar görmesin.
            await CloseSiblingsAsync(report);

            return true;
        }

        /// <summary>
        /// Hedefin sahibini bulur; hedef yoksa <c>null</c>. Sahip bilgisi bildirimle
        /// birlikte saklanır — içerik silinse bile kuyruk kime ait olduğunu bilir.
        /// </summary>
        private async Task<string?> ResolveTargetOwnerAsync(CreateReportRequest request)
        {
            switch (request.TargetType)
            {
                case ReportTargetType.Review:
                    return await _context.Reviews
                        .Where(r => r.Id == request.TargetId)
                        .Select(r => r.UserId)
                        .FirstOrDefaultAsync();

                case ReportTargetType.ReviewComment:
                    return await _context.ReviewComments
                        .Where(c => c.Id == request.TargetId)
                        .Select(c => c.UserId)
                        .FirstOrDefaultAsync();

                case ReportTargetType.UserList:
                    return await _context.UserLists
                        .Where(l => l.Id == request.TargetId)
                        .Select(l => l.UserId)
                        .FirstOrDefaultAsync();

                case ReportTargetType.User:
                {
                    if (string.IsNullOrWhiteSpace(request.TargetUsername))
                        return null;

                    var normalized = request.TargetUsername.ToUpperInvariant();
                    return await _context.Users
                        .Where(u => u.NormalizedUserName == normalized || u.UserName == request.TargetUsername)
                        .Select(u => u.Id)
                        .FirstOrDefaultAsync();
                }

                default:
                    return null;
            }
        }

        /// <summary>Kuyruktaki kartlar için içerik metni ve bağlantısı; silinmiş içerik boş kalır.</summary>
        private async Task<Dictionary<(ReportTargetType, int), (string? Excerpt, string? Url)>> LoadPreviewsAsync(
            List<Report> reports)
        {
            var result = new Dictionary<(ReportTargetType, int), (string?, string?)>();

            var reviewIds = Ids(reports, ReportTargetType.Review);
            if (reviewIds.Count > 0)
            {
                var rows = await _context.Reviews
                    .Where(r => reviewIds.Contains(r.Id))
                    .Select(r => new { r.Id, r.Body, r.Show!.TmdbId })
                    .ToListAsync();

                foreach (var row in rows)
                    result[(ReportTargetType.Review, row.Id)] = (Excerpt(row.Body), $"/show/{row.TmdbId}");
            }

            var commentIds = Ids(reports, ReportTargetType.ReviewComment);
            if (commentIds.Count > 0)
            {
                var rows = await _context.ReviewComments
                    .Where(c => commentIds.Contains(c.Id))
                    .Select(c => new { c.Id, c.Body, c.Review!.Show!.TmdbId })
                    .ToListAsync();

                foreach (var row in rows)
                    result[(ReportTargetType.ReviewComment, row.Id)] = (Excerpt(row.Body), $"/show/{row.TmdbId}");
            }

            var listIds = Ids(reports, ReportTargetType.UserList);
            if (listIds.Count > 0)
            {
                var rows = await _context.UserLists
                    .Where(l => listIds.Contains(l.Id))
                    .Select(l => new { l.Id, l.Title, l.Description })
                    .ToListAsync();

                foreach (var row in rows)
                    result[(ReportTargetType.UserList, row.Id)] =
                        (Excerpt(row.Description is null ? row.Title : $"{row.Title} — {row.Description}"),
                         $"/list/{row.Id}");
            }

            return result;
        }

        private static List<int> Ids(List<Report> reports, ReportTargetType type) =>
            reports.Where(r => r.TargetType == type && r.TargetId != null)
                   .Select(r => r.TargetId!.Value)
                   .Distinct()
                   .ToList();

        /// <summary>Bildirilen içeriği kaldırır. Kullanıcı bildiriminde silinecek içerik yoktur.</summary>
        private async Task DeleteContentAsync(Report report)
        {
            if (report.TargetId == null)
                return;

            var targetId = report.TargetId.Value;

            switch (report.TargetType)
            {
                case ReportTargetType.Review:
                {
                    var review = await _context.Reviews.FirstOrDefaultAsync(r => r.Id == targetId);
                    if (review == null)
                        return;

                    _context.Reviews.Remove(review);
                    await _context.SaveChangesAsync();

                    // Beğeni/yorumlar FK cascade ile gider; olay ve bildirimlerin FK'si yok.
                    await _activityService.RemoveReviewedAsync(targetId);
                    await _notificationService.RemoveForReviewAsync(targetId);
                    break;
                }
                case ReportTargetType.ReviewComment:
                {
                    var comment = await _context.ReviewComments.FirstOrDefaultAsync(c => c.Id == targetId);
                    if (comment == null)
                        return;

                    _context.ReviewComments.Remove(comment);
                    await _context.SaveChangesAsync();
                    break;
                }
                case ReportTargetType.UserList:
                {
                    var list = await _context.UserLists.FirstOrDefaultAsync(l => l.Id == targetId);
                    if (list == null)
                        return;

                    _context.UserLists.Remove(list);
                    await _context.SaveChangesAsync();

                    await _notificationService.RemoveForListAsync(targetId);
                    break;
                }
            }
        }

        /// <summary>Aynı içerik için bekleyen diğer bildirimleri aynı kararla kapatır.</summary>
        private async Task CloseSiblingsAsync(Report resolved)
        {
            if (resolved.TargetId == null)
                return;

            var siblings = await _context.Reports
                .Where(r => r.Id != resolved.Id && r.Status == ReportStatus.Open
                         && r.TargetType == resolved.TargetType && r.TargetId == resolved.TargetId)
                .ToListAsync();
            if (siblings.Count == 0)
                return;

            foreach (var sibling in siblings)
            {
                sibling.Status = resolved.Status;
                sibling.ResolvedById = resolved.ResolvedById;
                sibling.ResolvedAt = resolved.ResolvedAt;
                sibling.ResolutionNote = resolved.ResolutionNote;
            }

            await _context.SaveChangesAsync();
        }

        private static string? Excerpt(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;

            return body.Length <= ExcerptLength ? body : body[..ExcerptLength].TrimEnd() + "…";
        }

        private static string? Clip(string? value)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return null;

            return trimmed.Length <= MaxNoteLength ? trimmed : trimmed[..MaxNoteLength];
        }
    }
}
