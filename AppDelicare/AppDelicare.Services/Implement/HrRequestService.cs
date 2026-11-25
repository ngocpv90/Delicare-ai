using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Interface
{
    /// <summary>UI-6.39 (D-120): "Nghỉ & đổi ca" – danh sách gộp, chỉ số, lịch nghỉ, xem trước hệ quả đơn nghỉ.</summary>
    public interface IHrRequestService
    {
        Task<IResult<LeaveImpactModel>> PreviewLeave(LeavePreviewRequest request);
        Task<IResults<HrRequestItemModel>> GetList(HrRequestListRequest request);
        Task<IResult<HrRequestSummaryModel>> GetSummary(Guid? organizationUnitId);
        Task<IResult<HrCalendarModel>> GetCalendar(DateTime? from, DateTime? to, Guid? organizationUnitId);
    }
}

namespace AppDelicare.Services.Implement
{
    public class HrRequestService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IPermissionService permissions, IOptions<BusinessRulesOptions> rules,
        IOptions<ReferenceListsOptions> referenceLists) : IHrRequestService
    {
        private readonly HrRulesOptions _hr = rules.Value.Hr;

        public static string? UnitCodeOf(DatabaseContext db, Guid? unitId) => unitId is { } u ? db.OrganizationUnit.AsNoTracking().Where(o => o.Id == u).Select(o => o.Code).FirstOrDefault() : null;

        /// <summary>Phép năm đã dùng (ngày, đơn Đã duyệt loại phép năm, trong năm) – đơn năm trước không tính.</summary>
        public static async Task<int> AnnualUsedAsync(DatabaseContext db, HrRulesOptions hr, Guid employeeId, int year, Guid? excludeId = null)
        {
            DateTime y0 = new(year, 1, 1), y1 = new(year, 12, 31);
            var rows = await db.LeaveRequest.AsNoTracking().Where(l => !l.IsDeleted && l.EmployeeId == employeeId && l.RequestStatus == LeaveRequestStatus.Approved
                && l.LeaveTypeCode == hr.AnnualLeaveTypeCode && l.FromDate <= y1 && l.ToDate >= y0 && (excludeId == null || l.Id != excludeId)).ToListAsync();
            return rows.Sum(l => { DateTime f = l.FromDate.Date > y0 ? l.FromDate.Date : y0, t = l.ToDate.Date < y1 ? l.ToDate.Date : y1; return t >= f ? (int)(t - f).TotalDays + 1 : 0; });
        }

        /// <summary>Hệ quả của đơn nghỉ: phép năm, ca bị bỏ, người cùng tổ nghỉ trùng, tổ còn x / y theo ngày × ca (Hr:ShiftMinStaff), người có thể thay.</summary>
        public static async Task<LeaveImpactModel> LeaveImpactAsync(DatabaseContext db, HrRulesOptions hr, Guid employeeId, DateTime from, DateTime to, string? leaveTypeCode, Guid? leaveId)
        {
            from = from.Date; to = to.Date;
            var m = new LeaveImpactModel { Days = to >= from ? (int)(to - from).TotalDays + 1 : 0, IsAnnual = string.Equals(leaveTypeCode, hr.AnnualLeaveTypeCode, StringComparison.OrdinalIgnoreCase) };
            m.AnnualTotal = hr.AnnualLeaveDays;
            m.AnnualUsed = await AnnualUsedAsync(db, hr, employeeId, from.Year, leaveId);
            m.AnnualRemaining = m.AnnualTotal - m.AnnualUsed;
            m.AnnualAfter = m.IsAnnual ? m.AnnualRemaining - m.Days : m.AnnualRemaining;
            m.AnnualOver = m.IsAnnual && m.AnnualAfter < 0;
            if (m.AnnualOver)
            {
                m.ListWarning.Add(string.Format(ConstantHelper.Message.HrAnnualOver, m.AnnualRemaining, m.Days));
            }
            var e = await db.Employee.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId);
            if (e == null || m.Days <= 0)
            {
                return m;
            }
            var mine = await db.ShiftAssignment.AsNoTracking().Where(a => !a.IsDeleted && a.EmployeeId == employeeId && a.WorkDate >= from && a.WorkDate <= to && ShiftAssignmentStatus.Open.Contains(a.AssignmentStatus!)).ToListAsync();
            m.ListShiftDrop = await HrRules.ModelsAsync(db, mine);
            if (e.OrganizationUnitId is not { } unit)
            {
                return m;
            }
            var team = await db.Employee.AsNoTracking().Where(x => !x.IsDeleted && x.OrganizationUnitId == unit && x.Id != employeeId && x.Status != EmployeeStatus.Resigned && x.Status != EmployeeStatus.Inactive).ToListAsync();
            var teamIds = team.Select(x => x.Id).ToList();
            var overlap = await HrRules.LeavesAsync(db, teamIds, from, to, false);
            m.ListOverlap = [.. overlap.Where(l => l.RequestStatus is LeaveRequestStatus.Approved or LeaveRequestStatus.Pending).Select(l => new LeaveOverlapModel
            {
                EmployeeId = l.EmployeeId, FullName = team.FirstOrDefault(t => t.Id == l.EmployeeId)?.FullName, FromDate = l.FromDate, ToDate = l.ToDate, Status = l.RequestStatus, StatusName = LeaveRequestStatus.Label(l.RequestStatus),
            })];
            var teamRows = await db.ShiftAssignment.AsNoTracking().Where(a => !a.IsDeleted && teamIds.Contains(a.EmployeeId) && a.WorkDate >= from && a.WorkDate <= to && a.AssignmentStatus != ShiftAssignmentStatus.Absent).ToListAsync();
            var shifts = await db.Shift.AsNoTracking().ToDictionaryAsync(s => s.Id);
            string? code = UnitCodeOf(db, unit);
            var conf = code != null ? hr.ShiftMinStaff.FirstOrDefault(k => string.Equals(k.Key, code, StringComparison.OrdinalIgnoreCase)).Value : null;
            var approvedOff = overlap.Where(l => l.RequestStatus == LeaveRequestStatus.Approved).ToList();
            foreach (var a in mine.OrderBy(a => a.WorkDate))
            {
                var s = shifts.GetValueOrDefault(a.ShiftId);
                int others = teamRows.Count(r => r.WorkDate.Date == a.WorkDate.Date && r.ShiftId == a.ShiftId);
                string dow = MenuService.MenuDayOf(a.WorkDate) switch { 6 => "T7", 7 => "CN", var n => "T" + (n + 1) };
                int? req = null;
                if (conf != null)
                {
                    var hit = conf.FirstOrDefault(k => string.Equals(k.Key, dow + ":" + s?.Code, StringComparison.OrdinalIgnoreCase));
                    if (hit.Key == null)
                    {
                        hit = conf.FirstOrDefault(k => string.Equals(k.Key, s?.Code, StringComparison.OrdinalIgnoreCase));
                    }
                    req = hit.Key == null ? null : hit.Value;
                }
                m.ListStaffing.Add(new LeaveStaffingModel { Day = a.WorkDate.Date, ShiftCode = s?.Code, Remaining = others, Assigned = others + 1, Required = req });
                var free = team.Where(t => !teamRows.Any(r => r.EmployeeId == t.Id && r.WorkDate.Date == a.WorkDate.Date)
                                           && !approvedOff.Any(l => l.EmployeeId == t.Id && l.FromDate.Date <= a.WorkDate.Date && l.ToDate.Date >= a.WorkDate.Date)).Take(8).ToList();
                m.ListReplacement.Add(new LeaveReplacementModel { Day = a.WorkDate.Date, ShiftCode = s?.Code, ListPerson = [.. free.Select(t => new HrPersonBriefModel { Id = t.Id, Code = t.Code ?? t.EmployeeCode, FullName = t.FullName })] });
            }
            foreach (var st in m.ListStaffing.Where(x => x.Short))
            {
                m.ListWarning.Add(string.Format(ConstantHelper.Message.HrLeaveTeamShort, st.ShiftCode, HrRules.Day(st.Day), st.Remaining, st.Required));
            }
            return m;
        }

        public async Task<IResult<LeaveImpactModel>> PreviewLeave(LeavePreviewRequest request)
        {
            try
            {
                var db = unitOfWork.DbContext;
                Guid? emp = request.EmployeeId is { } x && x != Guid.Empty ? x : (await HrRules.ResolveEmployeeAsync(db, currentActor.RequireActor().UserId))?.Id;
                var selfEmp = await HrRules.ResolveEmployeeAsync(db, currentActor.RequireActor().UserId);
                if (emp != null && emp != selfEmp?.Id && !permissions.IsAllowed(await permissions.GetAsync(currentActor.RequireActor().UserId), HrScreen.Leave, PermissionAction.View))
                {
                    return Result<LeaveImpactModel>.Fail(string.Format(ConstantHelper.Message.HrNoRightOther, "xem đơn nghỉ", "Nghỉ & đổi ca"));
                }
                if (emp == null)
                {
                    return Result<LeaveImpactModel>.Fail(ConstantHelper.Message.HrUserNoEmployee);
                }
                if (request.FromDate == default || request.ToDate == default || request.ToDate.Date < request.FromDate.Date)
                {
                    return Result<LeaveImpactModel>.Fail(string.Format(ConstantHelper.Message.HrDateRangeInvalid, _hr.LeaveMaxDays));
                }
                return Result<LeaveImpactModel>.Success(await LeaveImpactAsync(db, _hr, emp.Value, request.FromDate, request.ToDate, request.LeaveTypeCode, null), ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " PreviewLeave: ", ex.ToString()));
                throw;
            }
        }

        private string LeaveTypeName(string? code)
            => referenceLists.Value.Lists.TryGetValue("LeaveType", out var l) && l.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)) is { } i ? i.Label : code ?? string.Empty;

        private async Task<(bool Leave, bool Swap)> ViewRightsAsync()
        {
            var perms = await permissions.GetAsync(currentActor.RequireActor().UserId);
            return (permissions.IsAllowed(perms, HrScreen.Leave, PermissionAction.View), permissions.IsAllowed(perms, HrScreen.Swap, PermissionAction.View) || permissions.IsAllowed(perms, HrScreen.Leave, PermissionAction.View));
        }

        public async Task<IResults<HrRequestItemModel>> GetList(HrRequestListRequest request)
        {
            try
            {
                var db = unitOfWork.DbContext;
                DateTime now = clock.Now, today = clock.Today;
                Guid actor = currentActor.RequireActor().UserId;
                var self = await HrRules.ResolveEmployeeAsync(db, actor);
                var (canLeave, canSwap) = await ViewRightsAsync();
                var scope = await HrRules.OrganizationScopeAsync(db, request.OrganizationUnitId);
                var emps = await db.Employee.AsNoTracking().Where(e => !e.IsDeleted).ToDictionaryAsync(e => e.Id);
                var units = await db.OrganizationUnit.AsNoTracking().ToDictionaryAsync(o => o.Id, o => o.Name);
                bool InScope(Guid id) => scope == null || (emps.GetValueOrDefault(id)?.OrganizationUnitId is { } u && scope.Contains(u));
                string status = (request.Status ?? "PENDING").Trim().ToUpperInvariant();
                bool Match(string? s) => status switch
                {
                    "PENDING" => s is LeaveRequestStatus.Pending or ShiftSwapStatus.AwaitingConsent,
                    "APPROVED" => s == LeaveRequestStatus.Approved,
                    "CLOSED" => s is LeaveRequestStatus.Rejected or LeaveRequestStatus.Cancelled,
                    _ => true,
                };
                DateTime since = today.AddDays(-90);
                var list = new List<HrRequestItemModel>();
                var overdueHours = 24;
                string type = (request.Type ?? string.Empty).Trim().ToUpperInvariant();
                if (type is "" or "LEAVE")
                {
                    var leaves = await db.LeaveRequest.AsNoTracking().Where(l => !l.IsDeleted && (l.SubmittedAt >= since || l.ToDate >= today || l.RequestStatus == LeaveRequestStatus.Pending)).ToListAsync();
                    foreach (var l in leaves.Where(l => Match(l.RequestStatus) && InScope(l.EmployeeId) && (canLeave || l.EmployeeId == self?.Id)))
                    {
                        var e = emps.GetValueOrDefault(l.EmployeeId);
                        var item = new HrRequestItemModel
                        {
                            Id = l.Id, Type = "LEAVE", TypeName = "Xin nghỉ", RequestNo = l.RequestNo, EmployeeId = l.EmployeeId, EmployeeName = e?.FullName,
                            UnitName = e?.OrganizationUnitId is { } u ? units.GetValueOrDefault(u) : null,
                            Content = $"{LeaveTypeName(l.LeaveTypeCode)} {HrRules.Day(l.FromDate)}{(l.ToDate.Date != l.FromDate.Date ? " – " + HrRules.Day(l.ToDate) : string.Empty)} ({(int)(l.ToDate.Date - l.FromDate.Date).TotalDays + 1} ngày)",
                            SubmittedAt = l.SubmittedAt, Status = l.RequestStatus, StatusName = LeaveRequestStatus.Label(l.RequestStatus),
                            Overdue = l.RequestStatus == LeaveRequestStatus.Pending && l.SubmittedAt is { } sa && (now - sa).TotalHours > overdueHours,
                        };
                        if (l.RequestStatus == LeaveRequestStatus.Pending)
                        {
                            var impact = await LeaveImpactAsync(db, _hr, l.EmployeeId, l.FromDate, l.ToDate, l.LeaveTypeCode, l.Id);
                            if (impact.ListShiftDrop.Count > 0)
                            {
                                item.ListImpact.Add($"Bỏ {impact.ListShiftDrop.Count} ca");
                            }
                            item.ListImpact.AddRange(impact.ListStaffing.Where(x => x.Short).Select(x => $"Tổ thiếu {x.Required - x.Remaining} người {HrRules.Day(x.Day)[..5]} {x.ShiftCode}"));
                            if (impact.AnnualOver)
                            {
                                item.ListImpact.Add($"Vượt phép năm ({impact.AnnualRemaining} ngày còn)");
                            }
                        }
                        list.Add(item);
                    }
                }
                if (type is "" or "SWAP" or "GIVE")
                {
                    var swaps = await db.ShiftSwapRequest.AsNoTracking().Where(s => !s.IsDeleted && (s.SubmittedAt >= since || s.RequestStatus == LeaveRequestStatus.Pending || s.RequestStatus == ShiftSwapStatus.AwaitingConsent)).ToListAsync();
                    var aIds = swaps.SelectMany(s => new[] { s.CurrentShiftAssignmentId, s.ProposedShiftAssignmentId ?? Guid.Empty }).Where(x => x != Guid.Empty).Distinct().ToList();
                    var assigns = (await HrRules.ModelsAsync(db, await db.ShiftAssignment.AsNoTracking().Where(a => aIds.Contains(a.Id)).ToListAsync())).ToDictionary(a => a.Id);
                    foreach (var s in swaps.Where(s => Match(s.RequestStatus) && (InScope(s.RequesterEmployeeId) || InScope(s.TargetEmployeeId))
                                                       && (canSwap || s.RequesterEmployeeId == self?.Id || s.TargetEmployeeId == self?.Id)))
                    {
                        bool give = s.ProposedShiftAssignmentId == null;
                        if ((type == "SWAP" && give) || (type == "GIVE" && !give))
                        {
                            continue;
                        }
                        var cur = assigns.GetValueOrDefault(s.CurrentShiftAssignmentId);
                        var pro = s.ProposedShiftAssignmentId is { } pid ? assigns.GetValueOrDefault(pid) : null;
                        var e = emps.GetValueOrDefault(s.RequesterEmployeeId);
                        string target = emps.GetValueOrDefault(s.TargetEmployeeId)?.FullName ?? string.Empty;
                        var item = new HrRequestItemModel
                        {
                            Id = s.Id, Type = give ? "GIVE" : "SWAP", TypeName = give ? "Nhường ca" : "Đổi ca", RequestNo = s.RequestNo, EmployeeId = s.RequesterEmployeeId, EmployeeName = e?.FullName,
                            UnitName = e?.OrganizationUnitId is { } u ? units.GetValueOrDefault(u) : null,
                            Content = give ? $"Nhường ca {cur?.ShiftCode} {(cur != null ? HrRules.Day(cur.WorkDate) : "")} cho {target}"
                                : $"Ca {cur?.ShiftCode} {(cur != null ? HrRules.Day(cur.WorkDate) : "")} ⇄ ca {pro?.ShiftCode} {(pro != null ? HrRules.Day(pro.WorkDate) : "")} của {target}",
                            SubmittedAt = s.SubmittedAt, Status = s.RequestStatus, StatusName = ShiftSwapStatus.Label(s.RequestStatus),
                            Overdue = s.RequestStatus == LeaveRequestStatus.Pending && s.SubmittedAt is { } sa && (now - sa).TotalHours > overdueHours,
                        };
                        if (s.RequestStatus == ShiftSwapStatus.AwaitingConsent)
                        {
                            item.ListImpact.Add($"{target} chưa đồng ý");
                            if (s.TargetEmployeeId == self?.Id)
                            {
                                item.AllowedActions.Add("consent");
                            }
                        }
                        else if (s.RequestStatus == LeaveRequestStatus.Pending)
                        {
                            item.ListImpact.Add($"{target} đã đồng ý");
                        }
                        if (s.RequesterEmployeeId == self?.Id && s.RequestStatus is LeaveRequestStatus.Pending or ShiftSwapStatus.AwaitingConsent)
                        {
                            item.AllowedActions.Add("cancel");
                        }
                        list.Add(item);
                    }
                }
                if (!string.IsNullOrWhiteSpace(request.Keyword))
                {
                    string k = request.Keyword.Trim();
                    list = [.. list.Where(i => (i.EmployeeName ?? "").Contains(k, StringComparison.OrdinalIgnoreCase) || (i.RequestNo ?? "").Contains(k, StringComparison.OrdinalIgnoreCase) || (i.Content ?? "").Contains(k, StringComparison.OrdinalIgnoreCase))];
                }
                list = [.. list.OrderByDescending(i => i.Status is LeaveRequestStatus.Pending or ShiftSwapStatus.AwaitingConsent).ThenByDescending(i => i.Overdue).ThenByDescending(i => i.SubmittedAt)];
                return Results<HrRequestItemModel>.Success(list, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<HrRequestSummaryModel>> GetSummary(Guid? organizationUnitId)
        {
            try
            {
                var db = unitOfWork.DbContext;
                DateTime today = clock.Today, now = clock.Now;
                var scope = await HrRules.OrganizationScopeAsync(db, organizationUnitId);
                var emps = await db.Employee.AsNoTracking().Where(e => !e.IsDeleted).ToDictionaryAsync(e => e.Id);
                bool InScope(Guid id) => scope == null || (emps.GetValueOrDefault(id)?.OrganizationUnitId is { } u && scope.Contains(u));
                var m = new HrRequestSummaryModel();
                var pendLeave = await db.LeaveRequest.AsNoTracking().Where(l => !l.IsDeleted && l.RequestStatus == LeaveRequestStatus.Pending).ToListAsync();
                var pendSwap = await db.ShiftSwapRequest.AsNoTracking().Where(s => !s.IsDeleted && (s.RequestStatus == LeaveRequestStatus.Pending || s.RequestStatus == ShiftSwapStatus.AwaitingConsent)).ToListAsync();
                m.PendingLeave = pendLeave.Count(l => InScope(l.EmployeeId));
                m.PendingSwap = pendSwap.Count(s => InScope(s.RequesterEmployeeId));
                m.Pending = m.PendingLeave + m.PendingSwap;
                m.PendingOverdue = pendLeave.Count(l => InScope(l.EmployeeId) && l.SubmittedAt is { } a && (now - a).TotalHours > 24) + pendSwap.Count(s => InScope(s.RequesterEmployeeId) && s.SubmittedAt is { } a && (now - a).TotalHours > 24);
                var approved = (await HrRules.LeavesAsync(db, [.. emps.Keys], today, today.AddDays(7), true)).Where(l => InScope(l.EmployeeId)).ToList();
                m.ListOffToday = [.. approved.Where(l => l.FromDate.Date <= today && l.ToDate.Date >= today).Select(l => emps.GetValueOrDefault(l.EmployeeId)?.FullName ?? string.Empty).Distinct()];
                var next = approved.Where(l => l.ToDate.Date >= today.AddDays(1) && l.FromDate.Date <= today.AddDays(7)).ToList();
                m.OffNext7People = next.Select(l => l.EmployeeId).Distinct().Count();
                m.OffNext7Days = next.Sum(l => { DateTime f = l.FromDate.Date > today.AddDays(1) ? l.FromDate.Date : today.AddDays(1), t = l.ToDate.Date < today.AddDays(7) ? l.ToDate.Date : today.AddDays(7); return t >= f ? (int)(t - f).TotalDays + 1 : 0; });
                // Ca trống do nghỉ: ca bị bỏ theo đơn nghỉ đã duyệt (xóa mềm, ghi chú) từ hôm nay mà ngày đó tổ chưa có người thay (không có ca cùng ca / ngày được thêm sau).
                var removed = await db.ShiftAssignment.AsNoTracking().Where(a => a.IsDeleted && a.WorkDate >= today && a.Note != null && a.Note.Contains("Nghỉ theo đơn")).ToListAsync();
                foreach (var a in removed.Where(a => InScope(a.EmployeeId)))
                {
                    var unit = emps.GetValueOrDefault(a.EmployeeId)?.OrganizationUnitId;
                    bool covered = await db.ShiftAssignment.AsNoTracking().AnyAsync(x => !x.IsDeleted && x.WorkDate == a.WorkDate && x.ShiftId == a.ShiftId && x.EmployeeId != a.EmployeeId && x.CreatedDate > a.ModifiedDate
                        && db.Employee.Any(e => e.Id == x.EmployeeId && e.OrganizationUnitId == unit));
                    if (!covered)
                    {
                        m.EmptyShifts++;
                        if (!m.ListEmptyShiftDay.Contains(a.WorkDate.Date))
                        {
                            m.ListEmptyShiftDay.Add(a.WorkDate.Date);
                        }
                    }
                }
                m.ListEmptyShiftDay = [.. m.ListEmptyShiftDay.OrderBy(d => d)];
                return Result<HrRequestSummaryModel>.Success(m, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetSummary: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<HrCalendarModel>> GetCalendar(DateTime? from, DateTime? to, Guid? organizationUnitId)
        {
            try
            {
                var db = unitOfWork.DbContext;
                DateTime f = (from ?? ShiftAssignmentService.WeekStartOf(clock.Today)).Date, t = (to ?? f.AddDays(13)).Date;
                if ((t - f).TotalDays > 62 || t < f)
                {
                    return Result<HrCalendarModel>.Fail(string.Format(ConstantHelper.Message.HrDateRangeInvalid, 62));
                }
                var scope = await HrRules.OrganizationScopeAsync(db, organizationUnitId);
                var emps = await db.Employee.AsNoTracking().Where(e => !e.IsDeleted).ToDictionaryAsync(e => e.Id);
                var ids = emps.Values.Where(e => scope == null || (e.OrganizationUnitId is { } u && scope.Contains(u))).Select(e => e.Id).ToList();
                var leaves = (await HrRules.LeavesAsync(db, ids, f, t, false)).Where(l => l.RequestStatus is LeaveRequestStatus.Approved or LeaveRequestStatus.Pending).ToList();
                var swaps = await (from s in db.ShiftSwapRequest.AsNoTracking()
                                   join a in db.ShiftAssignment.AsNoTracking() on s.CurrentShiftAssignmentId equals a.Id
                                   where !s.IsDeleted && (s.RequestStatus == LeaveRequestStatus.Pending || s.RequestStatus == ShiftSwapStatus.AwaitingConsent || s.RequestStatus == LeaveRequestStatus.Approved)
                                         && a.WorkDate >= f && a.WorkDate <= t && ids.Contains(s.RequesterEmployeeId)
                                   select new { s.Id, s.RequesterEmployeeId, s.TargetEmployeeId, a.WorkDate, s.ProposedShiftAssignmentId }).ToListAsync();
                var units = await db.OrganizationUnit.AsNoTracking().ToDictionaryAsync(o => o.Id, o => new { o.Name, o.Code });
                var m = new HrCalendarModel { From = f, To = t };
                for (DateTime d = f; d <= t; d = d.AddDays(1))
                {
                    var day = new HrCalendarDayModel { Day = d };
                    foreach (var l in leaves.Where(l => l.FromDate.Date <= d && l.ToDate.Date >= d))
                    {
                        day.ListEntry.Add(new HrCalendarEntryModel { Id = l.Id, Kind = l.RequestStatus == LeaveRequestStatus.Approved ? "LEAVE_APPROVED" : "LEAVE_PENDING", FullName = emps.GetValueOrDefault(l.EmployeeId)?.FullName, Detail = LeaveTypeName(l.LeaveTypeCode) });
                    }
                    foreach (var s in swaps.Where(s => s.WorkDate.Date == d))
                    {
                        day.ListEntry.Add(new HrCalendarEntryModel { Id = s.Id, Kind = "SWAP", FullName = emps.GetValueOrDefault(s.RequesterEmployeeId)?.FullName, Detail = (s.ProposedShiftAssignmentId == null ? "nhường ca cho " : "đổi ca với ") + emps.GetValueOrDefault(s.TargetEmployeeId)?.FullName });
                    }
                    // Tổ sẽ thiếu nếu duyệt hết đơn chờ.
                    foreach (var g in leaves.Where(l => l.FromDate.Date <= d && l.ToDate.Date >= d && emps.GetValueOrDefault(l.EmployeeId)?.OrganizationUnitId != null)
                                 .GroupBy(l => emps[l.EmployeeId].OrganizationUnitId!.Value))
                    {
                        var u = units.GetValueOrDefault(g.Key);
                        var conf = u?.Code != null ? _hr.ShiftMinStaff.FirstOrDefault(k => string.Equals(k.Key, u.Code, StringComparison.OrdinalIgnoreCase)).Value : null;
                        if (conf == null)
                        {
                            continue;
                        }
                        var off = g.Select(l => l.EmployeeId).ToHashSet();
                        var rows = await db.ShiftAssignment.AsNoTracking().Where(a => !a.IsDeleted && a.WorkDate == d && db.Employee.Any(e => e.Id == a.EmployeeId && e.OrganizationUnitId == g.Key)).ToListAsync();
                        var shifts = await db.Shift.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Code);
                        foreach (var sg in rows.GroupBy(r => r.ShiftId))
                        {
                            string? sc = shifts.GetValueOrDefault(sg.Key);
                            var hit = conf.FirstOrDefault(k => string.Equals(k.Key, sc, StringComparison.OrdinalIgnoreCase));
                            int left = sg.Count(r => !off.Contains(r.EmployeeId));
                            if (hit.Key != null && left < hit.Value)
                            {
                                day.ListShort.Add($"{u!.Name} {sc} thiếu {hit.Value - left}");
                            }
                        }
                    }
                    m.ListDay.Add(day);
                }
                return Result<HrCalendarModel>.Success(m, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetCalendar: ", ex.ToString()));
                throw;
            }
        }
    }
}
