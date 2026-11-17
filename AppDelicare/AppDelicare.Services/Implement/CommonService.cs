#nullable enable
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE2.7 (spec 2.1 summary / export, 2.2 audit history – Catalog 2, 8, 15): API dùng chung, không gắn bảng (như DashboardService).
    /// Bảng / cột chỉ lấy từ <see cref="CommonRegistry"/>; quyền theo màn hình của danh sách (<see cref="CommonAccess"/>). Chỉ đọc.
    /// </summary>
    public partial class CommonService(IUnitOfWork unitOfWork, ICurrentActor currentActor, IPermissionService permissionService, IClock clock,
        IOptions<BusinessRulesOptions> rules, IMemoryCache cache, IApprovalRequestService approvalRequestService) : ICommonService
    {
        public const string SourceAudit = "AUDIT";
        public const string SourceApproval = "APPROVAL";
        public const string SourceComplaint = "COMPLAINT";
        public const string SourceFile = "FILE";

        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly IPermissionService _permissionService = permissionService;
        private readonly IClock _clock = clock;
        private readonly CommonRulesOptions _rules = rules.Value.Common;
        private readonly AlertRulesOptions _alertRules = rules.Value.Alerts;
        private readonly IMemoryCache _cache = cache;
        private readonly IApprovalRequestService _approvalRequestService = approvalRequestService;

        /// <summary>Cột hệ thống không hiện trong lịch sử (đã có ở thời điểm / người thao tác).</summary>
        private static readonly HashSet<string> HiddenAuditFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "Id", "CreatedDate", "CreatedBy", "ModifiedDate", "ModifiedBy", "DeletedDate", "DeleteddBy", "DeletedBy", "Actor",
        };

        /// <summary>Che giá trị: mật khẩu / băm, khóa bí mật, token, license, chuỗi kết nối, tọa độ GPS, khóa lưu trữ tệp (dòng cũ trước T45B.2 / BE2.6 vẫn có thể còn giá trị thật).</summary>
        [GeneratedRegex("pass(word)?|pwd|secret|token|api_?key|signing|licensekey|^license$|connectionstring|latitude|longitude|privatekey|configjson|credential|filenamehas", RegexOptions.IgnoreCase)]
        private static partial Regex SensitiveField();

        /// <summary>Dòng Changes: "- Field: From 'a' to 'b'" (sửa) / "- Field: 'v'" (thêm) / "- Field: (ghi chú)".</summary>
        [GeneratedRegex(@"^-\s*(?<f>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?:From '(?<from>.*)' to '(?<to>.*)'|'(?<val>.*)'|(?<raw>.*))$", RegexOptions.Singleline)]
        private static partial Regex ChangeLine();

        public static bool IsSensitive(string field) => SensitiveField().IsMatch(field);

        private static Result<T> Forbid<T>(string message) => new() { ResponseCode = CommonAccess.Forbidden, ResponseMessage = message };

        public async Task<IResults<CommonResourceModel>> GetResources()
        {
            try
            {
                var actor = _currentActor.RequireActor();
                var perms = await _permissionService.GetAsync(actor.UserId);
                var list = CommonRegistry.All.Where(r => _permissionService.IsAllowed(perms, r.ScreenId, PermissionAction.View)).Select(r => new CommonResourceModel
                {
                    Resource = r.Key,
                    Title = r.Title,
                    ScreenId = r.ScreenId,
                    StatusColumn = r.StatusColumn,
                    DateColumn = r.DateColumn,
                    ListSearchColumn = [.. r.SearchColumns],
                    ListExportColumn = [.. r.ExportColumns.Select(c => c.Header)],
                    Attachable = r.Attachable,
                }).ToList();
                return Results<CommonResourceModel>.Success(list, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetResources: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<StatusSummaryModel>> GetStatusSummary(CommonListRequest request)
        {
            try
            {
                var resource = CommonRegistry.Find(request.Resource);
                if (resource == null)
                {
                    return Result<StatusSummaryModel>.Fail(string.Format(ConstantHelper.Message.CommonResourceNotSupported, request.Resource));
                }
                if (await CommonAccess.DenyAsync(_permissionService, _currentActor, resource.ScreenId, PermissionAction.View) is { } deny)
                {
                    return Forbid<StatusSummaryModel>(deny);
                }
                if (resource.StatusColumn == null)
                {
                    return Result<StatusSummaryModel>.Fail(string.Format(ConstantHelper.Message.CommonNoStatusColumn, resource.Key));
                }
                var db = _unitOfWork.DbContext;
                var counts = await resource.CountByStatusAsync(db, request);
                // Đủ mọi mã của STATUS_CATALOG (kể cả 0 – chip "Lọc nhanh" luôn đủ), rồi mã lạ trong dữ liệu.
                var byCode = counts.Where(c => c.Status != null).GroupBy(c => c.Status!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Count), StringComparer.OrdinalIgnoreCase);
                var list = resource.CatalogStatuses().Select(code => new StatusCountModel { Status = code, StatusName = resource.StatusLabel(code), Count = byCode.GetValueOrDefault(code) }).ToList();
                foreach (var extra in byCode.Where(k => !list.Any(l => l.Status!.Equals(k.Key, StringComparison.OrdinalIgnoreCase))).OrderBy(k => k.Key))
                {
                    list.Add(new StatusCountModel { Status = extra.Key, StatusName = resource.StatusLabel(extra.Key), Count = extra.Value });
                }
                int empty = counts.Where(c => c.Status == null).Sum(c => c.Count);
                if (empty > 0)
                {
                    list.Add(new StatusCountModel { Status = null, StatusName = "(Chưa có trạng thái)", Count = empty });
                }
                return Result<StatusSummaryModel>.Success(new StatusSummaryModel
                {
                    Resource = resource.Key,
                    StatusColumn = resource.StatusColumn,
                    Total = counts.Sum(c => c.Count),
                    ListByStatus = list,
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetStatusSummary: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<FileContentModel>> ExportList(CommonListRequest request)
        {
            try
            {
                var resource = CommonRegistry.Find(request.Resource);
                if (resource == null)
                {
                    return Result<FileContentModel>.Fail(string.Format(ConstantHelper.Message.CommonResourceNotSupported, request.Resource));
                }
                string format = string.IsNullOrWhiteSpace(request.Format) ? "xlsx" : request.Format.Trim().TrimStart('.').ToLowerInvariant();
                if (format != "xlsx" && format != "csv")
                {
                    return Result<FileContentModel>.Fail(string.Format(ConstantHelper.Message.CommonFormatInvalid, request.Format, "xlsx, csv"));
                }
                if (await CommonAccess.DenyAsync(_permissionService, _currentActor, resource.ScreenId, PermissionAction.Export) is { } deny)
                {
                    return Forbid<FileContentModel>(deny);
                }
                var db = _unitOfWork.DbContext;
                int max = Math.Max(1, _rules.ExportMaxRows);
                int total = await resource.CountAsync(db, request);
                var rows = await resource.ListAsync(db, request, max);
                var table = await BuildTableAsync(resource, rows);
                string? note = total > rows.Count ? string.Format(ConstantHelper.Message.CommonExportTruncated, rows.Count) : null;
                string stamp = _clock.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);
                var file = format == "csv"
                    ? new FileContentModel { Content = ToCsv(table, note), ContentType = "text/csv; charset=utf-8", FileName = $"{resource.Key}_{stamp}.csv" }
                    : new FileContentModel { Content = ToXlsx(resource, table, note, _clock.Now), ContentType = ConstantHelper.DownloadExcel, FileName = $"{resource.Key}_{stamp}.xlsx" };
                return Result<FileContentModel>.Success(file, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportList: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Bảng xuất: tiêu đề + giá trị (trạng thái → nhãn, khóa ngoại → tên / số, số giữ kiểu số cho Excel).</summary>
        private async Task<(List<string> Headers, List<object?[]> Rows)> BuildTableAsync(CommonResource resource, List<BaseEntity> rows)
        {
            var db = _unitOfWork.DbContext;
            var props = resource.ExportColumns.Select(c => resource.EntityType.GetProperty(c.Property, BindingFlags.Public | BindingFlags.Instance)!).ToArray();
            var labels = new Dictionary<string, Dictionary<Guid, string>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < props.Length; i++)
            {
                if (CommonRegistry.Lookups.TryGetValue(props[i].Name, out var lookup))
                {
                    var ids = rows.Select(r => props[i].GetValue(r) as Guid?).Where(x => x != null).Select(x => x!.Value).ToList();
                    labels[props[i].Name] = ids.Count == 0 ? [] : await lookup.LabelsAsync(db, ids);
                }
            }
            var data = rows.Select(r => props.Select(p =>
            {
                object? v = p.GetValue(r);
                if (v is Guid g)
                {
                    return labels.TryGetValue(p.Name, out var map) ? map.GetValueOrDefault(g, string.Empty) : string.Empty;
                }
                if (resource.StatusColumn != null && p.Name.Equals(resource.StatusColumn, StringComparison.OrdinalIgnoreCase))
                {
                    return resource.StatusLabel(v as string);
                }
                return v;
            }).ToArray()).ToList();
            return (["STT", .. resource.ExportColumns.Select(c => c.Header)], data);
        }

        private static byte[] ToXlsx(CommonResource resource, (List<string> Headers, List<object?[]> Rows) table, string? note, DateTime now)
        {
            const string brand = "#91AF42";
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(resource.Key.Length > 31 ? resource.Key[..31] : resource.Key);
            int cols = table.Headers.Count;
            ws.Range(1, 1, 1, cols).Merge().Value = resource.Title.ToUpper(CultureInfo.GetCultureInfo("vi-VN"));
            ws.Range(1, 1, 1, cols).Style.Font.SetBold().Font.SetFontSize(14).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Range(2, 1, 2, cols).Merge().Value = $"Xuất lúc {now:dd/MM/yyyy HH:mm} – {table.Rows.Count} dòng";
            ws.Range(2, 1, 2, cols).Style.Font.SetItalic().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            const int head = 4;
            for (int c = 0; c < cols; c++)
            {
                ws.Cell(head, c + 1).Value = table.Headers[c];
            }
            ws.Range(head, 1, head, cols).Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml(brand))
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetWrapText(true);
            int row = head;
            foreach (var r in table.Rows)
            {
                row++;
                ws.Cell(row, 1).Value = row - head;
                for (int c = 0; c < r.Length; c++)
                {
                    var cell = ws.Cell(row, c + 2);
                    switch (r[c])
                    {
                        case null: break;
                        case decimal m: cell.Value = m; break;
                        case int n: cell.Value = n; break;
                        case long l: cell.Value = l; break;
                        case double d: cell.Value = d; break;
                        // Chuỗi luôn ghi dạng chữ (ClosedXML không hiểu "=…" là công thức khi gán Value kiểu string) – chống chèn công thức.
                        default: cell.Value = CommonFormat.Text(r[c]); break;
                    }
                }
            }
            if (row > head)
            {
                ws.Range(head, 1, row, cols).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorder(XLBorderStyleValues.Thin);
            }
            if (note != null)
            {
                ws.Cell(row + 2, 1).Value = note;
                ws.Cell(row + 2, 1).Style.Font.SetItalic().Font.SetFontColor(XLColor.Red);
            }
            ws.SheetView.FreezeRows(head);
            ws.Columns(1, cols).AdjustToContents(6.0, 45.0);
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.FitToPages(1, 0);
            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            return stream.ToArray();
        }

        /// <summary>CSV UTF-8 có BOM (Excel đọc đúng tiếng Việt); ô bắt đầu bằng = + - @ được thêm dấu ' (chống chèn công thức – CSV injection).</summary>
        public static byte[] ToCsv((List<string> Headers, List<object?[]> Rows) table, string? note)
        {
            static string Cell(string value)
            {
                if (value.Length > 0 && "=+-@\t\r".Contains(value[0]))
                {
                    value = "'" + value;
                }
                return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
            }
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", table.Headers.Select(Cell)));
            int i = 0;
            foreach (var r in table.Rows)
            {
                i++;
                sb.AppendLine(string.Join(",", new[] { i.ToString(CultureInfo.InvariantCulture) }.Concat(r.Select(v => Cell(CommonFormat.Text(v))))));
            }
            if (note != null)
            {
                sb.AppendLine(Cell(note));
            }
            return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
        }

        public async Task<IResults<RecordHistoryItemModel>> GetHistory(string? resourceName, Guid id)
        {
            try
            {
                var resource = CommonRegistry.Find(resourceName);
                if (resource == null)
                {
                    return Results<RecordHistoryItemModel>.Fail(string.Format(ConstantHelper.Message.CommonResourceNotSupported, resourceName));
                }
                if (await CommonAccess.DenyAsync(_permissionService, _currentActor, resource.ScreenId, PermissionAction.View) is { } deny)
                {
                    return new Results<RecordHistoryItemModel>([], 0) { ResponseCode = CommonAccess.Forbidden, ResponseMessage = deny };
                }
                var db = _unitOfWork.DbContext;
                // Bản ghi đã xóa mềm vẫn xem được lịch sử (để biết ai xóa) – chỉ cần đã từng tồn tại.
                bool known = await resource.ExistsAsync(db, id) || await db.AuditChange.AsNoTracking().AnyAsync(a => a.IdChange == id && a.EntityName == resource.EntityType.Name);
                if (!known)
                {
                    return Results<RecordHistoryItemModel>.Fail(string.Format(ConstantHelper.Message.CommonRecordNotFound, resource.Title));
                }
                var items = new List<RecordHistoryItemModel>();

                // 1. AuditChange của bản ghi + dòng con (dòng đơn, dòng phiếu…).
                var targets = new List<(string Entity, List<Guid> Ids)> { (resource.EntityType.Name, [id]) };
                foreach (var child in resource.Children)
                {
                    targets.Add((child.EntityType.Name, await child.IdsAsync(db, id)));
                }
                foreach (var (entity, ids) in targets.Where(t => t.Ids.Count > 0))
                {
                    foreach (var chunk in ids.Chunk(500))
                    {
                        var part = chunk.Select(x => (Guid?)x).ToList();
                        var audits = await db.AuditChange.AsNoTracking().Where(a => a.EntityName == entity && part.Contains(a.IdChange) && !a.IsDeleted).ToListAsync();
                        items.AddRange(audits.Select(a => AuditItem(a, entity)));
                    }
                }

                // 2. Yêu cầu duyệt của bản ghi (BusinessId) + quyết định từng cấp.
                var approvals = await db.ApprovalRequest.AsNoTracking().Where(r => r.BusinessId == id && !r.IsDeleted).ToListAsync();
                if (approvals.Count > 0)
                {
                    var approvalIds = approvals.Select(r => r.Id).ToList();
                    var decisions = await db.ApprovalDecision.AsNoTracking().Where(d => approvalIds.Contains(d.ApprovalRequestId) && !d.IsDeleted).ToListAsync();
                    var stepIds = decisions.Select(d => d.ApprovalStepId).Distinct().ToList();
                    var stepNo = await db.ApprovalStep.AsNoTracking().Where(s => stepIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.StepNo);
                    foreach (var r in approvals)
                    {
                        items.Add(new RecordHistoryItemModel
                        {
                            At = r.RequestedAt, By = r.RequestedBy, Source = SourceApproval, Action = "SUBMITTED", ActionName = "Gửi duyệt",
                            EntityName = nameof(ApprovalRequest), EntityId = r.Id, ReferenceNo = r.RequestNo, Comment = r.BusinessType,
                        });
                        foreach (var d in decisions.Where(d => d.ApprovalRequestId == r.Id))
                        {
                            int? no = stepNo.TryGetValue(d.ApprovalStepId, out var n) ? n : null;
                            items.Add(new RecordHistoryItemModel
                            {
                                At = d.DecidedAt, By = d.DecidedBy, Source = SourceApproval, Action = d.DecisionCode, ActionName = DecisionLabel(d.DecisionCode) + (no != null ? $" (cấp {no})" : string.Empty),
                                EntityName = nameof(ApprovalDecision), EntityId = d.Id, ReferenceNo = r.RequestNo, Comment = d.Comment,
                            });
                        }
                        bool decidedAtEnd = decisions.Any(d => d.ApprovalRequestId == r.Id && r.CompletedAt != null && Math.Abs((d.DecidedAt - r.CompletedAt.Value).TotalSeconds) < 2);
                        if (r.CompletedAt is { } done && !decidedAtEnd)
                        {
                            items.Add(new RecordHistoryItemModel
                            {
                                At = done, By = r.ModifiedBy, Source = SourceApproval, Action = r.RequestStatus?.ToUpperInvariant(),
                                ActionName = "Kết thúc yêu cầu duyệt: " + CommonRegistry.Find(nameof(ApprovalRequest))!.StatusLabel(r.RequestStatus),
                                EntityName = nameof(ApprovalRequest), EntityId = r.Id, ReferenceNo = r.RequestNo,
                            });
                        }
                    }
                }

                // 3. Phản ánh khách hàng: ComplaintHistory.
                if (resource.EntityType == typeof(CustomerComplaint))
                {
                    var history = await db.ComplaintHistory.AsNoTracking().Where(h => h.ComplaintId == id && !h.IsDeleted).ToListAsync();
                    var complaintStatus = CommonRegistry.Find(nameof(CustomerComplaint))!;
                    items.AddRange(history.Select(h => new RecordHistoryItemModel
                    {
                        At = h.EventAt, By = h.ActorId ?? h.CreatedBy, Source = SourceComplaint, Action = h.EventType, ActionName = h.EventType,
                        EntityName = nameof(ComplaintHistory), EntityId = h.Id, Comment = h.Comment,
                        ListChange = h.OldStatus == h.NewStatus ? [] : [new RecordChangeModel { Field = "ComplaintStatus", From = complaintStatus.StatusLabel(h.OldStatus), To = complaintStatus.StatusLabel(h.NewStatus) }],
                    }));
                }

                // 4. Tệp đính kèm của bản ghi (DocumentFile – khóa lưu trữ, GAP-DOC-01).
                string prefix = DocumentStorageRules.OwnerPrefix(resource.Key, id);
                var files = await db.DocumentFile.AsNoTracking().Where(f => f.FileNameHas != null && f.FileNameHas.StartsWith(prefix)).ToListAsync();
                items.AddRange(files.Select(f => new RecordHistoryItemModel
                {
                    At = f.CreatedDate, By = f.CreatedBy, Source = SourceFile, Action = f.IsDeleted ? "UPLOAD_DELETED" : "UPLOAD",
                    ActionName = f.IsDeleted ? "Đính kèm tệp (đã xóa)" : "Đính kèm tệp", EntityName = nameof(DocumentFile), EntityId = f.Id,
                    Comment = (f.FileName ?? string.Empty) + (f.FileType ?? string.Empty),
                }));

                var ordered = items.OrderByDescending(i => i.At).ThenBy(i => i.Source).Take(Math.Max(1, _rules.HistoryMaxItems)).ToList();
                var names = await CommonAccess.UserNamesAsync(db, ordered.Select(i => i.By));
                foreach (var i in ordered)
                {
                    i.ByName = i.By is { } b ? names.GetValueOrDefault(b) : null;
                }
                return new Results<RecordHistoryItemModel>(ordered, items.Count) { ResponseCode = 200, ResponseMessage = ConstantHelper.Message.GetAllSuccessfully };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetHistory: ", ex.ToString()));
                throw;
            }
        }

        private static string DecisionLabel(string? code) => code?.ToUpperInvariant() switch
        {
            "APPROVED" or "APPROVE" => "Duyệt",
            "REJECTED" or "REJECT" => "Từ chối",
            "CANCELLED" or "CANCEL" => "Hủy",
            _ => code ?? string.Empty,
        };

        /// <summary>1 dòng AuditChange → mục lịch sử; cột hệ thống bị bỏ, cột nhạy cảm bị che (kể cả dòng ghi trước khi có che ở DbContext).</summary>
        public static RecordHistoryItemModel AuditItem(AuditChange a, string entity)
        {
            var changes = ParseChanges(a.Changes);
            bool softDelete = changes.Any(c => c.Field.Equals("IsDeleted", StringComparison.OrdinalIgnoreCase) && string.Equals(c.To, "True", StringComparison.OrdinalIgnoreCase));
            string? action = softDelete ? "Deleted" : a.Action;
            return new RecordHistoryItemModel
            {
                At = a.CreatedDate, By = a.CreatedBy, Source = SourceAudit, Action = action,
                ActionName = action switch { "Added" => "Tạo mới", "Modified" => "Cập nhật", "Deleted" => "Xóa", "Download" => "Tải tệp", _ => action },
                EntityName = entity, EntityId = a.IdChange, ListChange = changes,
            };
        }

        public static List<RecordChangeModel> ParseChanges(string? changes)
        {
            var list = new List<RecordChangeModel>();
            if (string.IsNullOrWhiteSpace(changes))
            {
                return list;
            }
            foreach (var raw in changes.Split([ConstantHelper.Break, "\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var m = ChangeLine().Match(raw);
                if (!m.Success)
                {
                    continue;
                }
                string field = m.Groups["f"].Value;
                if (HiddenAuditFields.Contains(field))
                {
                    continue;
                }
                if (IsSensitive(field))
                {
                    list.Add(new RecordChangeModel { Field = field, From = null, To = "(đã ẩn)", Masked = true });
                    continue;
                }
                list.Add(m.Groups["from"].Success && m.Groups["to"].Success
                    ? new RecordChangeModel { Field = field, From = m.Groups["from"].Value, To = m.Groups["to"].Value }
                    : new RecordChangeModel { Field = field, To = m.Groups["val"].Success ? m.Groups["val"].Value : m.Groups["raw"].Value.Trim() });
            }
            return list;
        }
    }
}
