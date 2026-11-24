#nullable enable
using System.Net;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Implement.Approval;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// APR-UI-1 (D-81, anh chốt 2026-11-23): màn Phê duyệt tổng – khuôn tóm tắt chung (1.2), Tôi đã gửi / Tôi đã duyệt / duyệt nhiều / nhắc / thống kê (1.3),
    /// gom kế hoạch SX giữ Nháp chờ COO (D-81 g), người duyệt vắng leo thang ngay (1.4), tổng hợp cuối ngày (1.7). Không bảng / cột mới:
    /// dấu "đã nhắc" / "người duyệt vắng" = dòng <c>ApprovalItem</c> (ActionCode REMIND / ABSENT) của chính yêu cầu.
    /// </summary>
    public partial class ApprovalRequestService
    {
        /// <summary>ApprovalItem.ActionCode – lần nhắc người duyệt (D-81 h).</summary>
        public const string RemindActionCode = "REMIND";
        /// <summary>ApprovalItem.ActionCode – đã leo thang vì người duyệt vắng (D-81 i), PayloadJson {date, step}.</summary>
        public const string AbsentActionCode = "ABSENT";

        // ------------------------------------------------------------ 1.2 khuôn tóm tắt

        /// <summary>Khuôn tóm tắt của 1 yêu cầu: handler.GetSummaryAsync (lỗi / chưa viết → mặc định) + mặc định chung + cảnh báo "người gửi bị từ chối ≥ N lần" + "người duyệt vắng".</summary>
        private async Task<ApprovalSummaryModel> BuildSummaryAsync(IApprovalHandler? handler, ApprovalRequest r, int stepNo, int stepCount, string? screen)
        {
            ApprovalSummaryModel? s = null;
            var db = _unitOfWork.DbContext;
            Guid bid = r.BusinessId ?? Guid.Empty;
            if (handler != null && bid != Guid.Empty)
            {
                try
                {
                    s = await handler.GetSummaryAsync(bid, new ApprovalSummaryContext
                    {
                        Db = db, Rules = _rules, Now = _clock.Now, RequestedBy = r.RequestedBy, ProposalJson = UnwrapPayload(r.PayloadJson).Proposal,
                        StepNo = stepNo, StepCount = stepCount, StepScreenId = screen,
                    });
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[ApprovalRequestService] GetSummaryAsync {Type} {Id}: lỗi – dùng khuôn mặc định", r.BusinessType, bid);
                }
            }
            s ??= new ApprovalSummaryModel();
            var (typeName, module, link) = ApprovalSummaries.TypeOf(r.BusinessType, stepNo, stepCount, screen);
            s.TypeName ??= typeName;
            s.ModuleName ??= module;
            s.OpenLink ??= link;
            s.DocumentNo ??= r.RequestNo;
            if (string.IsNullOrWhiteSpace(s.Title))
            {
                s.Title = (handler != null && bid != Guid.Empty ? await handler.DescribeAsync(bid) : null) ?? r.RequestNo;
            }
            s.Reason ??= "Cần duyệt theo quy trình";
            if (r.RequestedBy is { } by && _rules.ApprovalFlagRejectCount > 0)
            {
                DateTime since = _clock.Now.AddDays(-Math.Max(1, _rules.ApprovalFlagRejectDays));
                int rejected = await db.ApprovalRequest.AsNoTracking().CountAsync(a => !a.IsDeleted && a.Id != r.Id && a.RequestedBy == by && a.BusinessType == r.BusinessType
                    && a.RequestStatus == Rejected && a.CompletedAt >= since);
                if (rejected >= _rules.ApprovalFlagRejectCount)
                {
                    s.Flags.Add(new ApprovalFlagModel { Text = $"Người gửi bị từ chối loại này {rejected} lần trong {_rules.ApprovalFlagRejectDays} ngày" });
                }
            }
            string today = _clock.Today.ToString("yyyy-MM-dd");
            if (await db.ApprovalItem.AsNoTracking().AnyAsync(i => i.ApprovalRequestId == r.Id && !i.IsDeleted && i.ActionCode == AbsentActionCode && i.PayloadJson != null && i.PayloadJson.Contains(today)))
            {
                s.Flags.Insert(0, new ApprovalFlagModel { Text = "Người duyệt cấp này đang nghỉ hôm nay – đã báo bậc leo thang", Severity = "high" });
            }
            s.Flags = [.. s.Flags.Take(3)];
            return s;
        }

        /// <summary>D-81 (d): duyệt nhiều – Bình thường + loại trong ApprovalBulkTypes; PO chỉ khi &lt; ngưỡng tự duyệt (PurchaseOrderAutoApproveMaxAmount &gt; 0).</summary>
        private async Task<bool> CanBulkAsync(string? businessType, Guid? businessId, ApprovalSummaryModel s)
        {
            if (s.Flags.Count > 0 || businessType == null || !_rules.ApprovalBulkTypes.Contains(businessType, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
            if (businessType == PurchaseOrderStatus.BusinessType)
            {
                decimal? amount = businessId is { } id ? await _unitOfWork.DbContext.PurchaseOrder.AsNoTracking().Where(p => p.Id == id).Select(p => p.TotalAmount).FirstOrDefaultAsync() : null;
                return _rules.PurchaseOrderAutoApproveMaxAmount > 0 && amount != null && amount < _rules.PurchaseOrderAutoApproveMaxAmount;
            }
            return true;
        }

        /// <summary>Hạn "sắp hết" (&lt; ApprovalDueSoonHours giờ, chưa quá hạn).</summary>
        private bool DueSoon(DateTime? due) => due != null && due >= _clock.Now && due < _clock.Now.AddHours(Math.Max(0, _rules.ApprovalDueSoonHours));

        // ------------------------------------------------------------ D-81 (g): kế hoạch SX giữ Nháp chờ COO

        private const string HeldMarker = "chờ COO duyệt";

        /// <summary>Kế hoạch Nháp mà lần tự chạy sau chốt đơn ghi "KHÔNG tự phát hành – chờ COO duyệt" (Note JSON AutoRun – BIZ-2.4).</summary>
        private async Task<List<(ProductionPlan Plan, string AutoRun)>> HeldPlansAsync(Guid? planId = null)
        {
            var rows = await _unitOfWork.DbContext.ProductionPlan.AsNoTracking()
                .Where(p => !p.IsDeleted && p.Status == ProductionPlanStatus.Draft && p.Note != null && p.Note.Contains(HeldMarker) && (planId == null || p.Id == planId))
                .OrderBy(p => p.ProductionDate).Take(50).ToListAsync();
            var result = new List<(ProductionPlan, string)>();
            foreach (var p in rows)
            {
                try
                {
                    string? auto = (string?)JObject.Parse(p.Note!)["AutoRun"];
                    if (auto != null && auto.Contains(HeldMarker, StringComparison.Ordinal))
                    {
                        result.Add((p, auto));
                    }
                }
                catch (Newtonsoft.Json.JsonException)
                {
                }
            }
            return result;
        }

        private async Task<ApprovalPendingModel> PlanPendingAsync(ProductionPlan plan, string autoRun)
        {
            var s = await ApprovalSummaries.HeldPlanAsync(plan, _unitOfWork.DbContext, autoRun);
            (s.TypeName, s.ModuleName) = ("Kế hoạch sản xuất", "Sản xuất");
            DateTime at = plan.ModifiedDate ?? plan.CreatedDate;
            // Hạn = trước ngày sản xuất (00:00) – kế hoạch phải phát hành trước khi nấu.
            DateTime due = plan.ProductionDate.Date;
            return new ApprovalPendingModel
            {
                Id = plan.Id, RequestNo = plan.PlanNumber ?? plan.PlanNo, BusinessType = ApprovalSummaries.PlanBusinessType, BusinessId = plan.Id, ScreenId = "prod-plan",
                Title = s.Title, RequestedAt = at, RequestedByName = "Hệ thống (sau chốt đơn)", RequestStatus = Pending, StepNo = 1, StepCount = 1, StepName = "Kế hoạch sản xuất",
                DueAt = due, IsOverdue = due < _clock.Now, DueSoon = DueSoon(due), Summary = s, CanBulk = false, Source = "PLAN",
            };
        }

        /// <summary>Gom kế hoạch HELD vào danh sách chờ (người có quyền Duyệt prod-plan; lọc theo type nếu có).</summary>
        private async Task AppendHeldPlansAsync(List<ApprovalPendingModel> list, UserPermissions perms, string? type)
        {
            if ((!string.IsNullOrEmpty(type) && !type.Equals("prod-plan", StringComparison.OrdinalIgnoreCase))
                || !_permissionService.IsAllowed(perms, "prod-plan", PermissionAction.Approve))
            {
                return;
            }
            foreach (var (plan, auto) in await HeldPlansAsync())
            {
                list.Add(await PlanPendingAsync(plan, auto));
            }
        }

        /// <summary>D-81 (b): quá hạn → sắp hết hạn → cũ nhất.</summary>
        private static List<ApprovalPendingModel> SortPending(IEnumerable<ApprovalPendingModel> list)
            => [.. list.OrderByDescending(x => x.IsOverdue).ThenByDescending(x => x.DueSoon).ThenBy(x => x.RequestedAt)];

        /// <summary>Decide trên kế hoạch HELD (Id = kế hoạch): duyệt = ProductionPlan/Approve; từ chối không có.</summary>
        private async Task<IResult<bool>?> DecidePlanAsync(ApprovalDecideRequest request)
        {
            var held = await HeldPlansAsync(request.Id);
            if (held.Count == 0)
            {
                return null;
            }
            var perms = await _permissionService.GetAsync(_currentActor.RequireActor().UserId);
            if (!_permissionService.IsAllowed(perms, "prod-plan", PermissionAction.Approve))
            {
                return Result<bool>.Fail(ConstantHelper.Message.ApprovalNoRight, false);
            }
            if (!request.Approve)
            {
                return Result<bool>.Fail(ConstantHelper.Message.ApprovalPlanNoReject, false);
            }
            var r = await _serviceProvider.GetRequiredService<IProductionPlanService>().Approve(new ProductionActionRequest { Id = request.Id, Note = request.Note });
            return r.ResponseCode == Convert.ToInt32(HttpStatusCode.OK) ? Result<bool>.Success(true, r.ResponseMessage ?? ConstantHelper.Message.SaveSuccess) : Result<bool>.Fail(r.ResponseMessage ?? ConstantHelper.Message.InternalServer, false);
        }

        private async Task<ApprovalDetailModel?> PlanDetailAsync(Guid id)
        {
            var held = await HeldPlansAsync(id);
            if (held.Count == 0)
            {
                return null;
            }
            var perms = await _permissionService.GetAsync(_currentActor.RequireActor().UserId);
            bool can = _permissionService.IsAllowed(perms, "prod-plan", PermissionAction.Approve);
            if (!can && !_permissionService.IsAllowed(perms, "prod-plan", PermissionAction.View))
            {
                return null;
            }
            var p = await PlanPendingAsync(held[0].Plan, held[0].AutoRun);
            return new ApprovalDetailModel
            {
                Request = new ApprovalRequest { Id = p.Id, RequestNo = p.RequestNo, BusinessType = p.BusinessType, BusinessId = p.BusinessId, RequestStatus = Pending, RequestedAt = p.RequestedAt, DueAt = p.DueAt },
                StatusName = StatusLabel(Pending), RequestedByName = p.RequestedByName, ScreenId = "prod-plan", Title = p.Title, Summary = p.Summary,
                CanDecide = can, CanCancel = false, CurrentStepNo = 1, StepCount = 1, DueAt = p.DueAt, IsOverdue = p.IsOverdue,
                ListStepInfo = [new ApprovalStepInfoModel { StepNo = 1, ScreenId = "prod-plan", StepName = "Kế hoạch sản xuất (COO)", DecisionStatus = StepWaiting, DecisionStatusName = StepStatusLabel(StepWaiting), IsCurrent = true, DueAt = p.DueAt, IsOverdue = p.IsOverdue }],
            };
        }

        // ------------------------------------------------------------ 1.3 Tôi đã gửi / Tôi đã duyệt / duyệt nhiều / nhắc / thống kê

        public async Task<IResults<ApprovalSentModel>> GetMySent()
        {
            try
            {
                Guid me = _currentActor.RequireActor().UserId;
                var db = _unitOfWork.DbContext;
                var rows = await db.ApprovalRequest.AsNoTracking().Where(a => !a.IsDeleted && a.RequestedBy == me && OpenStatuses.Contains(a.RequestStatus))
                    .OrderBy(a => a.RequestedAt).Take(100).ToListAsync();
                var ids = rows.Select(r => r.Id).ToList();
                var allSteps = await db.ApprovalStep.AsNoTracking().Where(s => ids.Contains(s.ApprovalRequestId) && !s.IsDeleted).ToListAsync();
                var reminds = await LastRemindsAsync(ids);
                var routes = await RoutesAsync(ids);
                DateTime now = _clock.Now;
                var list = new List<ApprovalSentModel>();
                foreach (var r in rows)
                {
                    var h = HandlerOf(r.BusinessType);
                    var steps = allSteps.Where(s => s.ApprovalRequestId == r.Id).ToList();
                    var step = CurrentStep(steps);
                    int stepNo = step?.StepNo ?? r.CurrentStepNo ?? 1, stepCount = Math.Max(routes[r.Id]?.Count ?? 0, Math.Max(steps.Count, 1));
                    string? screen = h == null ? null : await StepScreenAsync(h, r.BusinessId, stepNo, routes[r.Id]);
                    var holders = screen == null ? [] : (await RecipientResolver.UsersWithApproveAsync(db, screen)).Where(u => u != me).ToList();
                    var names = (await UserNamesAsync(holders.Take(4))).Values.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
                    DateTime? last = reminds.TryGetValue(r.Id, out var lr) ? lr : null;
                    DateTime? due = step?.DueAt ?? r.DueAt;
                    list.Add(new ApprovalSentModel
                    {
                        Id = r.Id, RequestNo = r.RequestNo, BusinessType = r.BusinessType, BusinessId = r.BusinessId,
                        Summary = await BuildSummaryAsync(h, r, stepNo, stepCount, screen), StepNo = stepNo, StepCount = stepCount,
                        StepName = screen == null ? null : (await StepNamesAsync([screen])).GetValueOrDefault(screen, screen),
                        HolderNames = holders.Count == 0 ? "Chưa có người được phân quyền" : string.Join(", ", names.Take(3)) + (holders.Count > 3 ? $" và {holders.Count - 3} người khác" : string.Empty),
                        RequestedAt = r.RequestedAt, WaitingHours = Math.Round((now - (step?.AssignedAt ?? r.RequestedAt)).TotalHours, 1), DueAt = due, IsOverdue = due != null && due < now,
                        LastRemindAt = last, CanRemind = holders.Count > 0 && (last == null || last.Value.AddHours(Math.Max(0, _rules.ApprovalRemindHours)) <= now),
                    });
                }
                return new Results<ApprovalSentModel> { ResponseCode = Convert.ToInt32(HttpStatusCode.OK), ResponseMessage = ConstantHelper.Message.GetAllSuccessfully, ListData = list, TotalRecords = list.Count };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMySent: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResults<ApprovalDecidedModel>> GetMyDecided(int page, int pageSize)
        {
            try
            {
                Guid me = _currentActor.RequireActor().UserId;
                var db = _unitOfWork.DbContext;
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 100);
                var q = from d in db.ApprovalDecision.AsNoTracking()
                        join r in db.ApprovalRequest.AsNoTracking() on d.ApprovalRequestId equals r.Id
                        join s in db.ApprovalStep.AsNoTracking() on d.ApprovalStepId equals s.Id into ss
                        from s in ss.DefaultIfEmpty()
                        where d.DecidedBy == me && !d.IsDeleted && !r.IsDeleted && (d.DecisionCode == "APPROVE" || d.DecisionCode == "REJECT")
                        select new { d, r, StepNo = s != null ? s.StepNo : 1 };
                int total = await q.CountAsync();
                var rows = await q.OrderByDescending(x => x.d.DecidedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
                var list = new List<ApprovalDecidedModel>();
                foreach (var x in rows)
                {
                    var h = HandlerOf(x.r.BusinessType);
                    list.Add(new ApprovalDecidedModel
                    {
                        Id = x.r.Id, RequestNo = x.r.RequestNo, BusinessType = x.r.BusinessType, TypeName = ApprovalSummaries.TypeOf(x.r.BusinessType).TypeName,
                        Title = h != null && x.r.BusinessId is { } bid ? await h.DescribeAsync(bid) : x.r.RequestNo,
                        DecisionCode = x.d.DecisionCode, DecisionName = x.d.DecisionCode == "APPROVE" ? "Đã duyệt" : "Từ chối", DecidedAt = x.d.DecidedAt, Comment = x.d.Comment,
                        StepNo = x.StepNo, RequestStatus = StatusLabel(x.r.RequestStatus),
                    });
                }
                return new Results<ApprovalDecidedModel> { ResponseCode = Convert.ToInt32(HttpStatusCode.OK), ResponseMessage = ConstantHelper.Message.GetAllSuccessfully, ListData = list, TotalRecords = total };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMyDecided: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResults<ApprovalBulkResultModel>> DecideBulk(ApprovalBulkRequest request)
        {
            try
            {
                var pending = (await GetMyPending(null)).ListData?.ToDictionary(p => p.Id) ?? [];
                var list = new List<ApprovalBulkResultModel>();
                foreach (Guid id in request.ListId.Distinct())
                {
                    if (!pending.TryGetValue(id, out var p))
                    {
                        list.Add(new ApprovalBulkResultModel { Id = id, Ok = false, Message = ConstantHelper.Message.ApprovalBulkNotPending });
                        continue;
                    }
                    if (!p.CanBulk)
                    {
                        list.Add(new ApprovalBulkResultModel { Id = id, RequestNo = p.RequestNo, Ok = false, Message = ConstantHelper.Message.ApprovalBulkNotAllowed });
                        continue;
                    }
                    var r = await Decide(new ApprovalDecideRequest { Id = id, Approve = true, Note = request.Note });
                    list.Add(new ApprovalBulkResultModel { Id = id, RequestNo = p.RequestNo, Ok = r.ResponseCode == Convert.ToInt32(HttpStatusCode.OK), Message = r.ResponseMessage });
                }
                int ok = list.Count(x => x.Ok);
                return new Results<ApprovalBulkResultModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK), ResponseMessage = $"Đã duyệt {ok}/{list.Count} yêu cầu", ListData = list, TotalRecords = list.Count,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " DecideBulk: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<bool>> Remind(ApprovalRemindRequest request)
        {
            try
            {
                Guid me = _currentActor.RequireActor().UserId;
                var db = _unitOfWork.DbContext;
                var r = await db.ApprovalRequest.FirstOrDefaultAsync(a => a.Id == request.Id && !a.IsDeleted);
                if (r == null)
                {
                    return Result<bool>.Fail(ConstantHelper.Message.RecordNotFoundMessage, false);
                }
                if (!IsOpen(r.RequestStatus))
                {
                    return Result<bool>.Fail(string.Format(ConstantHelper.Message.ApprovalNotPending, StatusLabel(r.RequestStatus)), false);
                }
                if (r.RequestedBy != me)
                {
                    return Result<bool>.Fail(ConstantHelper.Message.ApprovalRemindOnlyRequester, false);
                }
                DateTime now = _clock.Now;
                DateTime? last = (await LastRemindsAsync([r.Id])).TryGetValue(r.Id, out var lr) ? lr : null;
                if (last != null && last.Value.AddHours(Math.Max(0, _rules.ApprovalRemindHours)) > now)
                {
                    return Result<bool>.Fail(string.Format(ConstantHelper.Message.ApprovalRemindTooSoon, last, last.Value.AddHours(_rules.ApprovalRemindHours)), false);
                }
                var handler = HandlerOf(r.BusinessType);
                if (handler == null || r.BusinessId is not { } bid)
                {
                    return Result<bool>.Fail($"Chưa khai báo xử lý duyệt cho loại {r.BusinessType}.", false);
                }
                var steps = await db.ApprovalStep.AsNoTracking().Where(s => s.ApprovalRequestId == r.Id && !s.IsDeleted).ToListAsync();
                var route = (await RoutesAsync([r.Id]))[r.Id];
                int stepNo = CurrentStep(steps)?.StepNo ?? r.CurrentStepNo ?? 1;
                string screen = await StepScreenAsync(handler, bid, stepNo, route);
                var approvers = (await RecipientResolver.UsersWithApproveAsync(db, screen)).Where(u => u != me).ToList();
                if (approvers.Count == 0)
                {
                    return Result<bool>.Fail(ConstantHelper.Message.ApprovalRemindNoApprover, false);
                }
                var summary = await BuildSummaryAsync(handler, r, stepNo, Math.Max(route?.Count ?? 0, Math.Max(steps.Count, 1)), screen);
                string? requester = (await UserNamesAsync([me])).GetValueOrDefault(me);
                _unitOfWork.ApprovalItemRepository.Add(new ApprovalItem
                {
                    Id = Guid.NewGuid(), ApprovalRequestId = r.Id, ItemType = r.BusinessType, ItemId = bid, ActionCode = RemindActionCode, ItemStatus = r.RequestStatus,
                    PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(new { step = stepNo, at = now, by = me, count = approvers.Count }),
                });
                await _unitOfWork.ApprovalItemRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                await BusinessNotifier.SafeEnqueueAsync(_notifications, new NotificationRequest
                {
                    Template = NotificationCatalog.ApprovalPending, UserIds = approvers,
                    Data = new
                    {
                        title = "🔔 Người gửi nhắc – " + summary.TypeName + ": " + summary.Title, requestNo = r.RequestNo, requester,
                        step = (await StepNamesAsync([screen])).GetValueOrDefault(screen, screen), dueAt = CurrentStep(steps)?.DueAt ?? r.DueAt,
                    },
                    Buttons = new[] { new NotificationButton("📄 Xem & duyệt", BusinessNotifier.Callback("apv", "view", r.Id)) },
                    IdempotencyKey = $"{NotificationCatalog.ApprovalPending}:{r.Id:N}:remind:{now:yyyyMMddHHmm}", CorrelationId = "APR:" + r.Id.ToString("N"),
                });
                return Result<bool>.Success(true, string.Format(ConstantHelper.Message.ApprovalReminded, approvers.Count));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Remind: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Lần nhắc gần nhất theo giờ nghiệp vụ (PayloadJson.at – IClock; CreatedDate do DbContext gán theo giờ máy).</summary>
        private async Task<Dictionary<Guid, DateTime>> LastRemindsAsync(List<Guid> ids)
        {
            var items = await _unitOfWork.DbContext.ApprovalItem.AsNoTracking().Where(i => ids.Contains(i.ApprovalRequestId) && !i.IsDeleted && i.ActionCode == RemindActionCode)
                .Select(i => new { i.ApprovalRequestId, i.PayloadJson, i.CreatedDate }).ToListAsync();
            DateTime At(string? json, DateTime fallback)
            {
                try
                {
                    return json != null && (DateTime?)JObject.Parse(json)["at"] is { } at ? at : fallback;
                }
                catch (Newtonsoft.Json.JsonException)
                {
                    return fallback;
                }
            }
            return items.GroupBy(i => i.ApprovalRequestId).ToDictionary(g => g.Key, g => g.Max(x => At(x.PayloadJson, x.CreatedDate)));
        }

        public async Task<IResults<ApprovalStatsModel>> GetStats(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var perms = await _permissionService.GetAsync(_currentActor.RequireActor().UserId);
                if (!perms.IsAdmin && !_permissionService.IsAllowed(perms, "wf-inbox", PermissionAction.View))
                {
                    return new Results<ApprovalStatsModel> { ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest), ResponseMessage = ConstantHelper.Message.ApprovalNoRight, ListData = [] };
                }
                DateTime from = (fromDate ?? _clock.Today.AddDays(-30)).Date, to = (toDate ?? _clock.Today).Date.AddDays(1);
                var db = _unitOfWork.DbContext;
                var rows = await db.ApprovalRequest.AsNoTracking().Where(a => !a.IsDeleted && a.RequestedAt >= from && a.RequestedAt < to)
                    .Select(a => new { a.Id, a.BusinessType, a.BusinessId, a.RequestStatus, a.RequestedAt, a.CompletedAt }).ToListAsync();
                var bids = rows.Where(r => r.BusinessId != null).Select(r => r.BusinessId!.Value).Distinct().ToList();
                // Chứng từ đã từng bị từ chối / hủy trước lần được duyệt → không tính "nguyên trạng".
                var earlier = await db.ApprovalRequest.AsNoTracking().Where(a => !a.IsDeleted && a.BusinessId != null && bids.Contains(a.BusinessId.Value)
                        && (a.RequestStatus == Rejected || a.RequestStatus == Cancelled))
                    .Select(a => new { a.BusinessId, a.RequestedAt }).ToListAsync();
                var list = rows.GroupBy(r => r.BusinessType).Select(g =>
                {
                    var approved = g.Where(r => r.RequestStatus == Approved).ToList();
                    var decided = g.Where(r => r.RequestStatus is Approved or Rejected && r.CompletedAt != null).ToList();
                    int asIs = approved.Count(a => !earlier.Any(e => e.BusinessId == a.BusinessId && e.RequestedAt < a.RequestedAt));
                    int decidedCount = g.Count(r => r.RequestStatus is Approved or Rejected);
                    return new ApprovalStatsModel
                    {
                        BusinessType = g.Key, TypeName = ApprovalSummaries.TypeOf(g.Key).TypeName, RequestCount = g.Count(), ApprovedCount = approved.Count,
                        RejectedCount = g.Count(r => r.RequestStatus == Rejected), OpenCount = g.Count(r => IsOpen(r.RequestStatus)),
                        ApprovedAsIsPct = decidedCount == 0 ? null : Math.Round(asIs * 100m / decidedCount, 1),
                        AvgWaitHours = decided.Count == 0 ? null : Math.Round((decimal)decided.Average(r => (r.CompletedAt!.Value - r.RequestedAt).TotalHours), 1),
                    };
                }).OrderByDescending(x => x.RequestCount).ToList();
                return new Results<ApprovalStatsModel> { ResponseCode = Convert.ToInt32(HttpStatusCode.OK), ResponseMessage = ConstantHelper.Message.GetAllSuccessfully, ListData = list, TotalRecords = list.Count };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetStats: ", ex.ToString()));
                throw;
            }
        }

        // ------------------------------------------------------------ 1.4 người duyệt vắng

        /// <summary>Người dùng có đơn nghỉ Đã duyệt phủ hôm nay (nhân viên của tài khoản theo HrRules.ResolveEmployeeAsync).</summary>
        private async Task<HashSet<Guid>> OnLeaveTodayAsync(IEnumerable<Guid> userIds)
        {
            var db = _unitOfWork.DbContext;
            DateTime today = _clock.Today;
            var result = new HashSet<Guid>();
            foreach (Guid u in userIds.Distinct())
            {
                var emp = await HrRules.ResolveEmployeeAsync(db, u);
                if (emp != null && await db.LeaveRequest.AsNoTracking().AnyAsync(l => l.EmployeeId == emp.Id && !l.IsDeleted && l.RequestStatus == LeaveRequestStatus.Approved
                        && l.FromDate.Date <= today && l.ToDate.Date >= today))
                {
                    result.Add(u);
                }
            }
            return result;
        }

        public async Task<int> EscalateAbsentAsync(IReadOnlyCollection<string> escalateGroups, IReadOnlyCollection<string> topGroups)
        {
            var db = _unitOfWork.DbContext;
            var open = await db.ApprovalRequest.AsNoTracking().Where(a => !a.IsDeleted && OpenStatuses.Contains(a.RequestStatus) && a.BusinessId != null).ToListAsync();
            int sent = 0;
            foreach (var r in open)
            {
                try
                {
                    sent += await EscalateIfAbsentAsync(r, escalateGroups, topGroups);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[ApprovalRequestService] EscalateAbsent {Id}: lỗi – bỏ qua", r.Id);
                }
            }
            return sent;
        }

        /// <summary>
        /// D-81 (i): mọi người có quyền duyệt cấp đang chờ (trừ người gửi) đều nghỉ hôm nay → báo bậc kế (COO); COO cũng nghỉ hết → CEO. 1 lần / yêu cầu / cấp / ngày
        /// (ApprovalItem ABSENT + IdempotencyKey). Ủy quyền đích danh = GAP-APR-04 (R2).
        /// </summary>
        private async Task<int> EscalateIfAbsentAsync(ApprovalRequest r, IReadOnlyCollection<string> escalateGroups, IReadOnlyCollection<string> topGroups)
        {
            var handler = HandlerOf(r.BusinessType);
            if (handler == null || r.BusinessId is not { } bid)
            {
                return 0;
            }
            var db = _unitOfWork.DbContext;
            string today = _clock.Today.ToString("yyyy-MM-dd");
            if (await db.ApprovalItem.AsNoTracking().AnyAsync(i => i.ApprovalRequestId == r.Id && !i.IsDeleted && i.ActionCode == AbsentActionCode && i.PayloadJson != null && i.PayloadJson.Contains(today)
                    && i.PayloadJson.Contains("\"step\":" + (r.CurrentStepNo ?? 1))))
            {
                return 0;
            }
            var steps = await db.ApprovalStep.AsNoTracking().Where(s => s.ApprovalRequestId == r.Id && !s.IsDeleted).ToListAsync();
            var route = (await RoutesAsync([r.Id]))[r.Id];
            int stepNo = CurrentStep(steps)?.StepNo ?? r.CurrentStepNo ?? 1;
            string screen = await StepScreenAsync(handler, bid, stepNo, route);
            var approvers = (await RecipientResolver.UsersWithApproveAsync(db, screen)).Where(u => u != r.RequestedBy).ToList();
            if (approvers.Count == 0)
            {
                return 0;
            }
            var absent = await OnLeaveTodayAsync(approvers);
            if (absent.Count < approvers.Count)
            {
                return 0;
            }
            var next = (await RecipientResolver.UsersInGroupsAsync(db, escalateGroups)).Where(u => u != r.RequestedBy).ToList();
            var nextAbsent = await OnLeaveTodayAsync(next);
            int level = 2;
            if (next.Count == 0 || nextAbsent.Count >= next.Count || next.All(absent.Contains))
            {
                next = (await RecipientResolver.UsersInGroupsAsync(db, topGroups)).Where(u => u != r.RequestedBy).ToList();
                level = 3;
            }
            next = [.. next.Where(u => !absent.Contains(u))];
            if (next.Count == 0)
            {
                return 0;
            }
            string stepName = (await StepNamesAsync([screen])).GetValueOrDefault(screen, screen);
            var names = (await UserNamesAsync(approvers.Take(3))).Values.Where(n => !string.IsNullOrWhiteSpace(n));
            _unitOfWork.ApprovalItemRepository.Add(new ApprovalItem
            {
                Id = Guid.NewGuid(), ApprovalRequestId = r.Id, ItemType = r.BusinessType, ItemId = bid, ActionCode = AbsentActionCode, ItemStatus = r.RequestStatus,
                PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(new { date = today, step = stepNo, level, absent = approvers }),
            });
            await _unitOfWork.ApprovalItemRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
            return await BusinessNotifier.SafeEnqueueAsync(_notifications, new NotificationRequest
            {
                Template = NotificationCatalog.ApprovalOverdue, UserIds = next, BotCode = MessagingCode.BotDieuHanh,
                Data = new { title = await handler.DescribeAsync(bid), requestNo = r.RequestNo, step = stepName, detail = $"Người duyệt cấp này đang nghỉ hôm nay ({string.Join(", ", names)}) – chuyển bạn xử lý (bậc {level})" },
                Buttons = new[] { new NotificationButton("📄 Xem & duyệt", BusinessNotifier.Callback("apv", "view", r.Id)) },
                IdempotencyKey = $"{NotificationCatalog.ApprovalOverdue}:{r.Id:N}:{stepNo}:absent:{today}", CorrelationId = "APR:" + r.Id.ToString("N"),
            });
        }

        /// <summary>Gọi sau khi giao cấp (gửi duyệt / chuyển cấp) – lỗi không làm hỏng việc gửi duyệt.</summary>
        private async Task SafeEscalateIfAbsentAsync(ApprovalRequest r)
        {
            try
            {
                var o = _serviceProvider.GetService<Microsoft.Extensions.Options.IOptionsMonitor<Model.Option.MessagingOptions>>()?.CurrentValue.Watch;
                await EscalateIfAbsentAsync(r, o?.EffectiveApprovalOverdueGroups ?? Model.Option.WatchOptions.DefaultApprovalOverdueGroups,
                    o?.EffectiveApprovalOverdueTopGroups ?? Model.Option.WatchOptions.DefaultApprovalOverdueTopGroups);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[ApprovalRequestService] kiểm người duyệt vắng {Id}: lỗi – bỏ qua", r.Id);
            }
        }

        // ------------------------------------------------------------ 1.7 tổng hợp cuối ngày

        public async Task<int> SendDigestAsync()
        {
            var db = _unitOfWork.DbContext;
            var open = await db.ApprovalRequest.AsNoTracking().Where(a => !a.IsDeleted && OpenStatuses.Contains(a.RequestStatus) && a.BusinessId != null).ToListAsync();
            if (open.Count == 0)
            {
                return 0;
            }
            var ids = open.Select(a => a.Id).ToList();
            var allSteps = await db.ApprovalStep.AsNoTracking().Where(s => ids.Contains(s.ApprovalRequestId) && !s.IsDeleted).ToListAsync();
            var routes = await RoutesAsync(ids);
            DateTime now = _clock.Now;
            // Người duyệt → (chờ, quá hạn, có cảnh báo).
            var per = new Dictionary<Guid, (int Wait, int Over, int Flag)>();
            foreach (var r in open)
            {
                var h = HandlerOf(r.BusinessType);
                if (h == null)
                {
                    continue;
                }
                var steps = allSteps.Where(s => s.ApprovalRequestId == r.Id).ToList();
                var step = CurrentStep(steps);
                int stepNo = step?.StepNo ?? r.CurrentStepNo ?? 1, stepCount = Math.Max(routes[r.Id]?.Count ?? 0, Math.Max(steps.Count, 1));
                string screen = await StepScreenAsync(h, r.BusinessId, stepNo, routes[r.Id]);
                var approvers = (await RecipientResolver.UsersWithApproveAsync(db, screen)).Where(u => u != r.RequestedBy).ToList();
                if (approvers.Count == 0)
                {
                    continue;
                }
                bool over = (step?.DueAt ?? r.DueAt) is { } due && due < now;
                bool flag = (await BuildSummaryAsync(h, r, stepNo, stepCount, screen)).Flags.Count > 0;
                foreach (var u in approvers)
                {
                    var c = per.GetValueOrDefault(u);
                    per[u] = (c.Wait + 1, c.Over + (over ? 1 : 0), c.Flag + (flag ? 1 : 0));
                }
            }
            int sent = 0;
            foreach (var (u, c) in per)
            {
                sent += await BusinessNotifier.SafeEnqueueAsync(_notifications, new NotificationRequest
                {
                    Template = NotificationCatalog.ApprovalDigest, UserIds = [u], BotCode = MessagingCode.BotDieuHanh,
                    Data = new
                    {
                        title = $"📋 Tổng hợp duyệt cuối ngày {now:dd/MM}",
                        lines = new[] { $"Còn chờ bạn: {c.Wait} ({c.Over} quá hạn, {c.Flag} có cảnh báo)" },
                    },
                    Buttons = new[] { new NotificationButton("⏳ Mở Phê duyệt", "menu:act:approvals") },
                    IdempotencyKey = $"{NotificationCatalog.ApprovalDigest}:{u:N}:{now:yyyyMMdd}",
                });
            }
            return sent;
        }
    }
}
