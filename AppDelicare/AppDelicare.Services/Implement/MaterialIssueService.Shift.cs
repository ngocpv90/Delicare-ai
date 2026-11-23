using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BIZ-2.5 (D-77 – C4): phiếu xuất Nháp theo ca do hệ thống tạo khi phát hành lệnh (thẻ <c>[XUAT_CA:&lt;số KH&gt;|&lt;mã ca&gt;]</c> đầu Note – GAP-BIZ-02).
    /// Kho xem theo nhóm ca, sửa số / lô từng phiếu như phiếu thường rồi <b>Ghi sổ cả ca</b> (mỗi phiếu ghi như MaterialIssue/Post – kiểm lô, tồn, trừ giữ chỗ).
    /// </summary>
    public partial class MaterialIssueService
    {
        private const string ShiftTagPrefix = "[XUAT_CA:";

        private static (string Tag, string? Plan, string? Shift)? ShiftTagOf(string? note)
        {
            if (string.IsNullOrEmpty(note) || !note.StartsWith(ShiftTagPrefix, StringComparison.Ordinal))
            {
                return null;
            }
            int end = note.IndexOf(']');
            if (end < 0)
            {
                return null;
            }
            string tag = note[..(end + 1)];
            var parts = note[ShiftTagPrefix.Length..end].Split('|');
            return (tag, parts.ElementAtOrDefault(0), parts.ElementAtOrDefault(1));
        }

        public async Task<IResults<MaterialIssueShiftGroupModel>> GetShiftGroups(DateTime? fromDate)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime from = (fromDate ?? _clock.Today.AddDays(-1)).Date;
                var issues = await db.MaterialIssue.AsNoTracking()
                    .Where(i => !i.IsDeleted && i.Note != null && i.Note.StartsWith(ShiftTagPrefix) && i.IssueDate >= from && i.Status != MaterialIssueStatus.Cancelled)
                    .ToListAsync();
                var orderIds = issues.Select(i => i.ProductionOrderId).Distinct().ToList();
                var orderNumbers = await db.ProductionOrder.AsNoTracking().Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.ProductionOrderNumber ?? o.ProductionOrderNo);
                var issueIds = issues.Select(i => i.Id).ToList();
                var lineCounts = await db.MaterialIssueLine.AsNoTracking().Where(l => issueIds.Contains(l.MaterialIssueId) && !l.IsDeleted)
                    .GroupBy(l => l.MaterialIssueId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
                var groups = issues.Select(i => (Issue: i, Tag: ShiftTagOf(i.Note))).Where(x => x.Tag != null)
                    .GroupBy(x => x.Tag!.Value.Tag)
                    .Select(g => new MaterialIssueShiftGroupModel
                    {
                        Tag = g.Key, PlanNumber = g.First().Tag!.Value.Plan, ShiftCode = g.First().Tag!.Value.Shift, IssueDate = g.Min(x => x.Issue.IssueDate),
                        DraftCount = g.Count(x => x.Issue.Status == MaterialIssueStatus.Draft), IssuedCount = g.Count(x => x.Issue.Status == MaterialIssueStatus.Issued),
                        ListIssue = [.. g.OrderBy(x => x.Issue.IssueNumber).Select(x => new MaterialIssueShiftItemModel
                        {
                            Id = x.Issue.Id, IssueNumber = x.Issue.IssueNumber ?? x.Issue.IssueNo, Status = x.Issue.Status,
                            ProductionOrderNumber = orderNumbers.GetValueOrDefault(x.Issue.ProductionOrderId), LineCount = lineCounts.GetValueOrDefault(x.Issue.Id),
                        })],
                    })
                    .OrderByDescending(g => g.DraftCount > 0).ThenBy(g => g.IssueDate).ThenBy(g => g.ShiftCode).ToList();
                return new Results<MaterialIssueShiftGroupModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK), ResponseMessage = ConstantHelper.Message.GetAllSuccessfully, ListData = groups, TotalRecords = groups.Count,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetShiftGroups: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<int>> PostShift(MaterialIssueShiftRequest request)
        {
            try
            {
                string tag = request.Tag.Trim();
                var ids = await _unitOfWork.DbContext.MaterialIssue.AsNoTracking()
                    .Where(i => !i.IsDeleted && i.Status == MaterialIssueStatus.Draft && i.Note != null && i.Note.StartsWith(tag))
                    .OrderBy(i => i.IssueNumber).Select(i => i.Id).ToListAsync();
                if (ids.Count == 0)
                {
                    return Result<int>.Fail(ConstantHelper.Message.ShiftIssueNothing, 0);
                }
                int posted = 0;
                var errors = new List<string>();
                foreach (Guid id in ids)
                {
                    var r = await Post(new InventoryDocumentActionRequest { Id = id, Note = "Ghi sổ cả ca" });
                    if (r.ResponseCode == 200)
                    {
                        posted++;
                    }
                    else
                    {
                        errors.Add((r.Data?.MaterialIssue?.IssueNumber ?? id.ToString("N")[..8]) + ": " + r.ResponseMessage);
                    }
                }
                string message = string.Format(ConstantHelper.Message.ShiftIssuePosted, posted, errors.Count == 0 ? string.Empty : $" – {errors.Count} phiếu lỗi: " + string.Join("; ", errors));
                return posted == 0 ? Result<int>.Fail(message, 0) : Result<int>.Success(posted, message);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " PostShift: ", ex.ToString()));
                throw;
            }
        }
    }
}
