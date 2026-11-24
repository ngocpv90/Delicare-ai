using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.14 (D-95): tab "Lịch sử" dạng dòng thời gian cho mọi trang chi tiết – <c>Common/GetTimeline</c>.
    /// Gộp nhật ký của bản ghi (cùng nguồn <c>Common/GetHistory</c>) + nhật ký của chứng từ liên quan (danh sách quan hệ <c>Common/GetRelated</c>,
    /// chỉ nhóm người dùng có quyền Xem), phân loại sự kiện (chấm màu + nhóm lọc), đổi trạng thái ra chữ, lọc nhóm / người ở máy chủ, tải dần 20 dòng.
    /// Không thêm bảng / cột.
    /// </summary>
    public partial class CommonService
    {
        /// <summary>Số chứng từ liên quan mới nhất mỗi nhóm được gộp nhật ký (chặn tải nặng với khách có hàng nghìn đơn).</summary>
        public const int TimelineRelatedPerGroup = 30;

        private static readonly HashSet<string> StatusFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "Status", "RequestStatus", "ComplaintStatus", "IssueStatus", "ContractStatus", "OrderStatus", "ApprovalStatus", "ReconciliationStatus",
        };

        private static readonly HashSet<string> RejectCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Cancelled", "Canceled", "Rejected", "Terminated", "Void", "Voided", "Failed", "Hold", "OnHold", "Inactive", "Closed_Rejected",
        };

        private static readonly HashSet<string> ApproveCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Approved", "Confirmed", "Signed", "Active", "Published", "Locked", "Completed", "Passed", "Accepted", "Reconciled", "Closed", "Done", "Delivered",
        };

        private static readonly HashSet<string> SubmitCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Submitted", "Pending", "PendingApproval", "InReview", "InProgress", "Sent",
        };

        public async Task<IResult<TimelineModel>> GetTimeline(TimelineRequest request)
        {
            try
            {
                var resource = CommonRegistry.Find(request.Resource);
                if (resource == null)
                {
                    return Result<TimelineModel>.Fail(string.Format(ConstantHelper.Message.CommonResourceNotSupported, request.Resource));
                }
                if (await CommonAccess.DenyAsync(_permissionService, _currentActor, resource.ScreenId, PermissionAction.View) is { } deny)
                {
                    return new Result<TimelineModel> { ResponseCode = CommonAccess.Forbidden, ResponseMessage = deny };
                }
                var db = _unitOfWork.DbContext;
                if (!await KnownAsync(db, resource, request.Id))
                {
                    return Result<TimelineModel>.Fail(string.Format(ConstantHelper.Message.CommonRecordNotFound, resource.Title));
                }
                var model = new TimelineModel();
                var all = new List<TimelineItemModel>();

                // 1. Chính bản ghi.
                foreach (var h in await CollectHistoryAsync(db, resource, request.Id))
                {
                    all.Add(ToTimeline(h, resource, request.Id, null, true));
                }

                // 2. Chứng từ liên quan (theo quyền Xem từng nhóm) – nhật ký của N chứng từ mới nhất mỗi nhóm.
                if (CommonRelations.FindRelatedSource(resource.Key) is { } source)
                {
                    var perms = await _permissionService.GetAsync(_currentActor.RequireActor().UserId);
                    int take = Math.Max(1, _alertRules.RelatedMaxItems);
                    foreach (var g in CommonRelations.Related[source.Key].Where(g => _permissionService.IsAllowed(perms, g.ScreenId, PermissionAction.View)))
                    {
                        var query = g.Query(db, request.Id);
                        int count = await query.CountAsync();
                        if (count == 0)
                        {
                            continue;
                        }
                        var rows = await query.OrderByDescending(r => r.At).ThenBy(r => r.Id).Take(Math.Max(take, TimelineRelatedPerGroup)).ToListAsync();
                        model.ListRelated.Add(new RelatedGroupModel
                        {
                            Key = g.Key, Title = g.Title, Route = "#/" + g.ScreenId, Count = count,
                            Items = [.. rows.Take(take).Select(r => new RelatedItemModel { Id = r.Id, Label = g.Label(r), Status = r.Status, StatusName = CommonRelations.StatusLabel(g.EntityType, g.StatusColumn, r.Status) })],
                        });
                        var relRes = CommonRegistry.Find(g.Key);
                        if (relRes == null)
                        {
                            continue;
                        }
                        foreach (var r in rows)
                        {
                            string label = r.Code ?? g.Label(r) ?? string.Empty;
                            foreach (var h in await CollectHistoryAsync(db, relRes, r.Id))
                            {
                                var t = ToTimeline(h, relRes, r.Id, label, false);
                                t.DocTitle = g.Title;
                                all.Add(t);
                            }
                        }
                    }
                }

                // 3. Tên người + "Hệ thống" (tài khoản system / không có người).
                var names = await CommonAccess.UserNamesAsync(db, all.Select(i => i.By));
                var systemIds = (await db.User.AsNoTracking().Where(u => u.UserName == "system").Select(u => u.Id).ToListAsync()).ToHashSet();
                foreach (var i in all)
                {
                    i.IsSystem = i.By == null || systemIds.Contains(i.By.Value);
                    i.ByName = i.IsSystem ? "Hệ thống" : names.GetValueOrDefault(i.By!.Value) ?? "Người dùng đã xóa";
                    if (i.IsSystem && i.Kind is "create" or "update")
                    {
                        i.Kind = "system";
                    }
                }
                model.ListActor = [.. all.GroupBy(i => i.IsSystem ? (Guid?)null : i.By).Select(g => new TimelineActorModel { Id = g.Key, Name = g.First().ByName ?? string.Empty, IsSystem = g.Key == null })
                    .OrderBy(a => a.IsSystem ? 1 : 0).ThenBy(a => a.Name)];

                // 4. Lọc người → đếm nhóm → lọc nhóm → tải dần.
                IEnumerable<TimelineItemModel> byActor = all;
                if (request.BySystem == true)
                {
                    byActor = all.Where(i => i.IsSystem);
                }
                else if (request.By is { } by)
                {
                    byActor = all.Where(i => !i.IsSystem && i.By == by);
                }
                var actorList = byActor.ToList();
                model.GroupCount["all"] = actorList.Count;
                foreach (var grp in new[] { "info", "status", "docs", "file" })
                {
                    model.GroupCount[grp] = actorList.Count(i => i.Group == grp);
                }
                string group = (request.Group ?? "all").Trim().ToLowerInvariant();
                var filtered = group == "all" ? actorList : actorList.Where(i => i.Group == group).ToList();
                int size = Math.Clamp(request.PageSize <= 0 ? 20 : request.PageSize, 1, 100);
                int page = Math.Max(1, request.Page);
                model.Total = filtered.Count;
                model.ListItem = [.. filtered.OrderByDescending(i => i.At).ThenBy(i => i.Source).Skip((page - 1) * size).Take(size)];
                model.HasMore = page * size < filtered.Count;
                await ResolveIdValuesAsync(db, model.ListItem);
                return Result<TimelineModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetTimeline: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Một dòng nhật ký → hoạt động của dòng thời gian (loại chấm, nhóm lọc, động từ, trạng thái ra chữ).</summary>
        public static TimelineItemModel ToTimeline(RecordHistoryItemModel h, CommonResource resource, Guid docId, string? docLabel, bool self)
        {
            var t = new TimelineItemModel
            {
                At = h.At, By = h.By, Source = h.Source, Action = h.Action, Comment = h.Comment, ReferenceNo = h.ReferenceNo,
                DocKey = resource.Key, DocId = docId, DocTitle = resource.Title, DocLabel = docLabel, IsSelf = self, EntityName = h.EntityName,
            };
            bool child = h.EntityName != null && h.EntityName != resource.EntityType.Name;
            foreach (var c in h.ListChange)
            {
                if (!child && StatusFields.Contains(c.Field) && !c.Masked)
                {
                    t.ListChange.Add(new RecordChangeModel
                    {
                        Field = c.Field, Masked = false,
                        From = c.From == null ? null : CommonRelations.StatusLabel(resource.EntityType, c.Field, c.From) ?? c.From,
                        To = c.To == null ? null : CommonRelations.StatusLabel(resource.EntityType, c.Field, c.To) ?? c.To,
                    });
                }
                else
                {
                    t.ListChange.Add(c);
                }
            }
            var statusChange = child ? null : h.ListChange.FirstOrDefault(c => StatusFields.Contains(c.Field));
            switch (h.Source)
            {
                case SourceFile:
                    t.Kind = "file";
                    t.Group = "file";
                    t.Verb = h.Action == "UPLOAD_DELETED" ? "đính kèm tệp (đã xóa)" : "đính kèm tệp";
                    break;
                case SourceApproval:
                    string code = (h.Action ?? string.Empty).ToUpperInvariant();
                    t.Kind = code switch { "SUBMITTED" => "submit", "APPROVED" or "APPROVE" => "approve", _ => "reject" };
                    t.Group = "status";
                    t.Verb = code == "SUBMITTED" ? "gửi duyệt" : (h.ActionName ?? code).ToLowerInvariant();
                    if (code == "SUBMITTED")
                    {
                        t.Comment = null; // BusinessType là mã kỹ thuật – không hiện.
                    }
                    break;
                case SourceComplaint:
                    t.Kind = (h.Action ?? string.Empty).Equals("REJECTED", StringComparison.OrdinalIgnoreCase) ? "reject" : "complaint";
                    t.Group = "status";
                    t.Verb = ComplaintVerb(h.Action);
                    break;
                default:
                    switch (h.Action)
                    {
                        case "Added":
                            t.Kind = "create";
                            t.Verb = child ? "thêm dòng" : self ? "tạo " + resource.Title.ToLowerInvariant() : "tạo";
                            break;
                        case "Deleted":
                            t.Kind = "reject";
                            t.Verb = child ? "xóa dòng" : "xóa";
                            break;
                        case "Download":
                            t.Kind = "file";
                            t.Group = "file";
                            t.Verb = "tải tệp";
                            break;
                        default:
                            if (statusChange != null)
                            {
                                t.Group = "status";
                                t.Kind = RejectCodes.Contains(statusChange.To ?? string.Empty) ? "reject"
                                    : ApproveCodes.Contains(statusChange.To ?? string.Empty) ? "approve"
                                    : SubmitCodes.Contains(statusChange.To ?? string.Empty) ? "submit" : "update";
                                t.Verb = "chuyển trạng thái";
                            }
                            else
                            {
                                t.Kind = "update";
                                t.Verb = child ? "sửa dòng" : "sửa thông tin";
                            }
                            break;
                    }
                    break;
            }
            if (!self)
            {
                t.Group = t.Group == "file" ? "file" : "docs";
                if (t.Kind is "create" or "update")
                {
                    t.Kind = resource.Key switch { "Order" => "order", "CustomerComplaint" => "complaint", _ => t.Kind };
                }
            }
            return t;
        }

/// <summary>Giá trị mã (Guid) của cột khóa ngoại → tên (CommonRegistry.Lookups); Guid không tra được → "(đã đổi)" – không bao giờ hiện Id.</summary>
        private static async Task ResolveIdValuesAsync(DatabaseContext db, List<TimelineItemModel> items)
        {
            var changes = items.SelectMany(i => i.ListChange).Where(c => !c.Masked).ToList();
            foreach (var byField in changes.GroupBy(c => c.Field, StringComparer.OrdinalIgnoreCase))
            {
                var ids = byField.SelectMany(c => new[] { c.From, c.To }).Select(v => Guid.TryParse(v, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).Distinct().ToList();
                if (ids.Count == 0)
                {
                    continue;
                }
                var labels = CommonRegistry.Lookups.TryGetValue(byField.Key, out var lookup) ? await lookup.LabelsAsync(db, ids) : [];
                foreach (var c in byField)
                {
                    c.From = Guid.TryParse(c.From, out var f) ? labels.GetValueOrDefault(f) ?? "(đã đổi)" : c.From;
                    c.To = Guid.TryParse(c.To, out var t) ? labels.GetValueOrDefault(t) ?? "(đã đổi)" : c.To;
                }
            }
        }

                private static string ComplaintVerb(string? eventType) => (eventType ?? string.Empty).ToUpperInvariant() switch
        {
            "CREATED" or "CREATE" or "OPENED" => "ghi nhận phản ánh",
            "ASSIGNED" or "ASSIGN" => "phân công xử lý phản ánh",
            "RESOLVED" or "RESOLVE" => "xử lý xong phản ánh",
            "CLOSED" or "CLOSE" => "đóng phản ánh",
            "REOPENED" or "REOPEN" => "mở lại phản ánh",
            "ESCALATED" or "ESCALATE" => "chuyển cấp phản ánh",
            "COMMENT" or "NOTE" => "ghi chú phản ánh",
            "REJECTED" or "REJECT" => "từ chối phản ánh",
            "STATUS" or "STATUS_CHANGED" => "đổi trạng thái phản ánh",
            _ => "cập nhật phản ánh",
        };
    }
}
