using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Implement.Messaging;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>UI-6.33 (D-114): thống kê sự cố theo NCC (theo loại, hướng xử lý) + nhắc sự cố quá hạn qua bot (BotWatch, 1 tin / ngày).</summary>
    public partial class SupplierIssueService
    {
        public sealed class SupplierIssueSummaryRow
        {
            public Guid SupplierId { get; set; }
            public string? SupplierName { get; set; }
            public int Total { get; set; }
            public int Open { get; set; }
            public int Overdue { get; set; }
            public DateTime? LastReportedAt { get; set; }
            public Dictionary<string, int> ByType { get; set; } = [];
            public Dictionary<string, int> ByResolution { get; set; } = [];
        }

        public async Task<IResult<List<SupplierIssueSummaryRow>>> GetSummary(DateTime? from, DateTime? to)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime dTo = (to ?? _clock.Today).Date, dFrom = (from ?? dTo.AddDays(-29)).Date, end = dTo.AddDays(1), now = _clock.Now;
                var issues = await db.SupplierIssue.AsNoTracking().Where(i => !i.IsDeleted && i.ReportedAt >= dFrom && i.ReportedAt < end).ToListAsync();
                var ids = issues.Select(i => i.SupplierId).Distinct().ToList();
                var names = await db.Supplier.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);
                var types = RefList("SupplierIssueType");
                var rows = issues.GroupBy(i => i.SupplierId).Select(g => new SupplierIssueSummaryRow
                {
                    SupplierId = g.Key, SupplierName = names.GetValueOrDefault(g.Key), Total = g.Count(),
                    Open = g.Count(i => i.IssueStatus is not (SupplierIssueStatus.Resolved or SupplierIssueStatus.Closed)), Overdue = g.Count(i => IsOverdue(i, now)),
                    LastReportedAt = g.Max(i => i.ReportedAt),
                    ByType = g.GroupBy(i => types.FirstOrDefault(t => t.Code == i.IssueType)?.Label ?? i.IssueType ?? "Khác").ToDictionary(x => x.Key, x => x.Count()),
                    ByResolution = g.Select(i => ParseResolution(i.Resolution).Name).Where(n => n != null).GroupBy(n => n!).ToDictionary(x => x.Key, x => x.Count()),
                }).OrderByDescending(r => r.Total).ThenBy(r => r.SupplierName).ToList();
                return Result<List<SupplierIssueSummaryRow>>.Success(rows, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetSummary: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Sự cố quá hạn chưa xử lý → 1 tin gộp / ngày tới người có quyền màn Sự cố NCC (QA, mua hàng; người phụ trách nằm trong nhóm này).</summary>
        public async Task<int> RemindOverdueAsync(DateTime now)
        {
            var db = _unitOfWork.DbContext;
            var late = (await db.SupplierIssue.AsNoTracking().Where(i => !i.IsDeleted && i.DueAt != null && i.DueAt < now
                    && i.IssueStatus != SupplierIssueStatus.Resolved && i.IssueStatus != SupplierIssueStatus.Closed).OrderBy(i => i.DueAt).Take(30).ToListAsync());
            if (late.Count == 0)
            {
                return 0;
            }
            var items = await ItemsAsync(late, now);
            return await BusinessNotifier.ToMenusAsync(_notifications, db, NotificationCatalog.SupplierIssueOverdue, ["#/qc-supplier-issue"],
                new
                {
                    title = $"{late.Count} sự cố nhà cung cấp quá hạn xử lý",
                    detail = "Mở Chất lượng & ATTP › Sự cố nhà cung cấp",
                    lines = items.Select(i => $"⏰ {i.SupplierName}: {i.IssueTypeName} – hạn {i.Issue.DueAt:dd/MM}" + (i.OwnerName == null ? " (chưa có người phụ trách)" : $" · {i.OwnerName}")).ToList(),
                },
                $"{NotificationCatalog.SupplierIssueOverdue}:{now:yyyyMMdd}");
        }
    }
}
