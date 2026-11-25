using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Implement.Messaging;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.37 (D-118): phân ca theo tuần Thứ 2 – CN – "Tô ca" theo lô (AssignMany), sao chép tuần trước (CopyWeek), tổng tổ × ngày × ca so với định biên cấu hình,
    /// số ca / giờ công tuần theo người, trạng thái tuần, ca đổi sau khi chốt (dấu trong Note – không thêm cột) → Confirm gửi SHIFT_CHANGED, nhắc lịch Thứ 6.
    /// </summary>
    public partial class ShiftAssignmentService
    {
        /// <summary>Dấu "đổi sau chốt" ghi vào Note của dòng ca đã Xác nhận bị đổi / bỏ: [Đổi sau chốt: SANG→TRUA] / [Đổi sau chốt: SANG→hủy]. Gửi xong → [Đã báo đổi: …].</summary>
        internal const string ChangedMark = "[Đổi sau chốt: ";
        internal const string NotifiedMark = "[Đã báo đổi: ";

        public static DateTime WeekStartOf(DateTime d) => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7));

        private static void MarkChanged(ShiftAssignment a, Shift? oldShift, Shift? newShift)
        {
            if (a.AssignmentStatus != ShiftAssignmentStatus.Confirmed || (a.Note ?? string.Empty).Contains(ChangedMark))
            {
                return;
            }
            string to = newShift == null ? "hủy" : newShift.Id == oldShift?.Id ? (newShift.Code + " đổi giờ") : newShift.Code ?? string.Empty;
            a.Note = HrRules.AppendNote(a.Note, $"{ChangedMark}{oldShift?.Code}→{to}]");
        }

        private static string? ChangedText(string? note)
        {
            int i = note?.IndexOf(ChangedMark, StringComparison.Ordinal) ?? -1;
            if (i < 0)
            {
                return null;
            }
            int j = note!.IndexOf(']', i);
            return j < 0 ? null : note[(i + ChangedMark.Length)..j];
        }

        /// <summary>Dòng ca (kể cả đã xóa mềm) mang dấu đổi sau chốt chưa báo, trong khoảng ngày.</summary>
        private async Task<List<ShiftAssignment>> ChangedRowsAsync(DateTime from, DateTime to, IReadOnlyCollection<Guid>? employeeIds)
            => await _unitOfWork.DbContext.ShiftAssignment.Where(a => a.WorkDate >= from && a.WorkDate <= to && a.Note != null && a.Note.Contains(ChangedMark)
                                                                    && (employeeIds == null || employeeIds.Contains(a.EmployeeId))).ToListAsync();

        // ================================================================ Lưới tuần

        private async Task WeekExtrasAsync(ShiftGridModel model, List<Employee> employees, List<Shift> shifts, bool week)
        {
            var db = _unitOfWork.DbContext;
            DateTime from = model.FromDate, to = model.ToDate, today = _clock.Today;
            if (week)
            {
                model.WeekStart = from;
            }
            var empIds = employees.Select(e => e.Id).ToList();
            var changed = await db.ShiftAssignment.AsNoTracking().Where(a => a.WorkDate >= from && a.WorkDate <= to && a.Note != null && a.Note.Contains(ChangedMark) && empIds.Contains(a.EmployeeId))
                .Select(a => new { a.EmployeeId, a.WorkDate, a.Note }).ToListAsync();
            var unitCodes = await db.OrganizationUnit.AsNoTracking().Where(o => !o.IsDeleted).ToDictionaryAsync(o => o.Id, o => o.Code);
            var activeShifts = shifts.Where(ShiftActive).OrderBy(s => s.StartTime).ToList();
            foreach (var row in model.Employees)
            {
                int count = 0;
                decimal hours = 0;
                foreach (var (day, cell) in row.Cells)
                {
                    if (changed.FirstOrDefault(c => c.EmployeeId == row.Id && Key(c.WorkDate) == day) is { } ch)
                    {
                        cell.ChangedAfterConfirm = true;
                        cell.ChangedFrom = ChangedText(ch.Note);
                    }
                    if (cell.AssignmentId != null && cell.Status != ShiftAssignmentStatus.Absent)
                    {
                        count++;
                        if (cell.PlannedStart is { } ps && cell.PlannedEnd is { } pe && pe > ps)
                        {
                            hours += (decimal)(pe - ps).TotalHours;
                        }
                    }
                }
                row.WeekShiftCount = count;
                row.WeekHours = Math.Round(hours, 1);
                if (week && count >= _hr.MaxShiftsPerWeek)
                {
                    row.Warnings.Add($"{count} ca / tuần (từ {_hr.MaxShiftsPerWeek} ca nên xem lại)");
                }
                if (week && row.Cells.Count == 7 && row.Cells.Values.All(c => c.AssignmentId != null))
                {
                    row.Warnings.Add("Không có ngày nghỉ trong tuần");
                }
            }
            // Tổng tổ × ngày × ca so với định biên.
            foreach (var g in model.Employees.GroupBy(e => e.OrganizationUnitId))
            {
                string? code = g.Key is { } uid ? unitCodes.GetValueOrDefault(uid) : null;
                var conf = code != null ? _hr.ShiftMinStaff.FirstOrDefault(k => string.Equals(k.Key, code, StringComparison.OrdinalIgnoreCase)).Value : null;
                foreach (string day in model.Days)
                {
                    DateTime d = DateTime.Parse(day, System.Globalization.CultureInfo.InvariantCulture);
                    string dow = MenuService.MenuDayOf(d) switch { 6 => "T7", 7 => "CN", _ => "T" + (MenuService.MenuDayOf(d) + 1) };
                    foreach (var s in activeShifts)
                    {
                        int assigned = g.Count(e => e.Cells.TryGetValue(day, out var c) && c.ShiftId == s.Id && c.Status != ShiftAssignmentStatus.Absent);
                        int? required = null;
                        if (conf != null)
                        {
                            var hit = conf.FirstOrDefault(k => string.Equals(k.Key, dow + ":" + s.Code, StringComparison.OrdinalIgnoreCase));
                            if (hit.Key == null)
                            {
                                hit = conf.FirstOrDefault(k => string.Equals(k.Key, s.Code, StringComparison.OrdinalIgnoreCase));
                            }
                            required = hit.Key == null ? null : hit.Value;
                        }
                        if (assigned > 0 || required != null)
                        {
                            model.UnitTotals.Add(new ShiftUnitTotalModel { UnitId = g.Key, UnitName = g.First().OrganizationUnitName, Day = day, ShiftId = s.Id, ShiftCode = s.Code, Assigned = assigned, Required = required });
                        }
                    }
                }
            }
            var cells = model.Employees.SelectMany(e => e.Cells.Values).ToList();
            int draft = cells.Count(c => c.Status == ShiftAssignmentStatus.Planned);
            int changedCount = changed.Select(c => (c.EmployeeId, c.WorkDate.Date)).Distinct().Count();
            int assignedCount = cells.Count(c => c.AssignmentId != null);
            model.WeekStatus = new ShiftWeekStatusModel { DraftCount = draft, ChangedAfterConfirmCount = changedCount, AssignedCount = assignedCount, Confirmed = assignedCount > 0 && draft == 0 && changedCount == 0 };
            model.Kpi = new ShiftWeekKpiModel
            {
                Assigned = assignedCount, Draft = draft,
                ShortSlots = model.UnitTotals.Count(t => t.Short && string.CompareOrdinal(t.Day, Key(today)) >= 0),
                PeopleWithoutShift = model.Employees.Count(e => e.Cells.Where(kv => string.CompareOrdinal(kv.Key, Key(today)) >= 0).All(kv => kv.Value.AssignmentId == null)
                                                               && e.Cells.Where(kv => string.CompareOrdinal(kv.Key, Key(today)) >= 0).Any(kv => !kv.Value.OnLeave)),
            };
        }

        // ================================================================ Tô ca theo lô

        public async Task<IResult<ShiftAssignManyResultModel>> AssignMany(ShiftAssignManyRequest request)
        {
            var cells = request.ListCell.Where(c => c.EmployeeId != Guid.Empty && c.WorkDate != default).ToList();
            if (cells.Count == 0)
            {
                return Result<ShiftAssignManyResultModel>.Fail(ConstantHelper.Message.HrNoEmployee);
            }
            if (cells.Count > 500)
            {
                return Result<ShiftAssignManyResultModel>.Fail(ConstantHelper.Message.HrAssignManyTooMany);
            }
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                Guid actor = _currentActor.RequireActor().UserId;
                var result = new ShiftAssignManyResultModel();
                foreach (var c in cells)
                {
                    var (_, error, _) = await AssignCoreAsync(new ShiftAssignRequest { EmployeeId = c.EmployeeId, WorkDate = c.WorkDate, ShiftId = c.ShiftId }, actor);
                    result.ListResult.Add(new ShiftCellResultModel { EmployeeId = c.EmployeeId, WorkDate = c.WorkDate.Date, ShiftId = c.ShiftId, Ok = error == null, Message = error });
                    if (error == null)
                    {
                        result.Done++;
                    }
                    else
                    {
                        result.Failed++;
                    }
                }
                _unitOfWork.CommitTransaction();
                return Result<ShiftAssignManyResultModel>.Success(result, string.Format(ConstantHelper.Message.HrAssignManyDone, result.Done, result.Failed));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " AssignMany: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        // ================================================================ Sao chép tuần

        public async Task<IResult<ShiftBulkAssignResultModel>> CopyWeek(ShiftCopyWeekRequest request)
        {
            if (request.FromWeekStart == default || request.ToWeekStart == default)
            {
                return Result<ShiftBulkAssignResultModel>.Fail(ConstantHelper.Message.ModelStateMessage);
            }
            DateTime src = WeekStartOf(request.FromWeekStart), dst = WeekStartOf(request.ToWeekStart), today = _clock.Today;
            if (src == dst)
            {
                return Result<ShiftBulkAssignResultModel>.Fail(ConstantHelper.Message.HrCopyWeekSame);
            }
            if (dst.AddDays(6) < today)
            {
                return Result<ShiftBulkAssignResultModel>.Fail(string.Format(ConstantHelper.Message.HrWorkDatePast, HrRules.Day(dst.AddDays(6))));
            }
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                Guid actor = _currentActor.RequireActor().UserId;
                var wanted = request.EmployeeIds.Where(x => x != Guid.Empty).Distinct().ToList();
                var employees = await EmployeesAsync(request.OrganizationUnitId, null, wanted.Count == 0 ? null : wanted);
                var empIds = employees.Select(e => e.Id).ToList();
                var source = await db.ShiftAssignment.AsNoTracking().Where(a => !a.IsDeleted && empIds.Contains(a.EmployeeId) && a.WorkDate >= src && a.WorkDate <= src.AddDays(6)).ToListAsync();
                var target = await db.ShiftAssignment.AsNoTracking().Where(a => !a.IsDeleted && empIds.Contains(a.EmployeeId) && a.WorkDate >= dst && a.WorkDate <= dst.AddDays(6)).ToListAsync();
                var shifts = await db.Shift.AsNoTracking().Where(s => !s.IsDeleted).ToDictionaryAsync(s => s.Id);
                var leaves = await HrRules.LeavesAsync(db, empIds, dst, dst.AddDays(6), true);
                var result = new ShiftBulkAssignResultModel();
                foreach (var a in source.OrderBy(a => a.WorkDate))
                {
                    var e = employees.First(x => x.Id == a.EmployeeId);
                    DateTime d = dst.AddDays((a.WorkDate.Date - src).TotalDays);
                    void Skip(string reason) => result.ListSkipped.Add(new ShiftBulkSkippedModel { EmployeeId = e.Id, EmployeeName = e.FullName, WorkDate = d, Reason = reason });
                    if (!shifts.TryGetValue(a.ShiftId, out var shift) || !ShiftActive(shift))
                    {
                        Skip(ConstantHelper.Message.HrShiftNotFound);
                        continue;
                    }
                    if (d < today)
                    {
                        Skip(string.Format(ConstantHelper.Message.HrWorkDatePast, HrRules.Day(d)));
                        continue;
                    }
                    if (target.FirstOrDefault(t => t.EmployeeId == e.Id && t.WorkDate.Date == d) is { } has)
                    {
                        if (has.ShiftId != a.ShiftId)
                        {
                            Skip(string.Format(ConstantHelper.Message.HrBulkSkipAssigned, shifts.GetValueOrDefault(has.ShiftId)?.Code));
                        }
                        continue;
                    }
                    if (leaves.FirstOrDefault(l => l.EmployeeId == e.Id && l.FromDate.Date <= d && l.ToDate.Date >= d) is { } leave)
                    {
                        Skip(string.Format(ConstantHelper.Message.HrBulkSkipLeave, leave.RequestNo));
                        continue;
                    }
                    var (start, end) = HrRules.PlannedWindow(d, shift);
                    Place(e.Id, shift, d, start, end, "Chép từ tuần " + HrRules.Day(src), actor);
                    result.Created++;
                }
                result.Skipped = result.ListSkipped.Count;
                await _unitOfWork.ShiftAssignmentRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<ShiftBulkAssignResultModel>.Success(result, string.Format(ConstantHelper.Message.HrCopyWeekDone, result.Created, HrRules.Day(dst), result.Skipped));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CopyWeek: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        // ================================================================ Gửi thay đổi sau chốt

        /// <summary>Mỗi nhân viên 1 tin SHIFT_CHANGED gom các ô đổi / hủy sau chốt; đổi dấu thành "Đã báo đổi" (gọi lại không gửi lại). Trả số người được báo.</summary>
        private async Task<(int People, HashSet<(Guid, DateTime)> Days)> NotifyChangedAsync(DateTime from, DateTime to, Guid? organizationUnitId)
        {
            var db = _unitOfWork.DbContext;
            var scope = await HrRules.OrganizationScopeAsync(db, organizationUnitId);
            var rows = await ChangedRowsAsync(from, to, null);
            if (scope != null)
            {
                var ids = rows.Select(r => r.EmployeeId).Distinct().ToList();
                var inScope = (await db.Employee.AsNoTracking().Where(e => ids.Contains(e.Id) && e.OrganizationUnitId != null).Select(e => new { e.Id, e.OrganizationUnitId }).ToListAsync())
                    .Where(e => scope.Contains(e.OrganizationUnitId!.Value)).Select(e => e.Id).ToHashSet();
                rows = [.. rows.Where(r => inScope.Contains(r.EmployeeId))];
            }
            var days = new HashSet<(Guid, DateTime)>();
            if (rows.Count == 0)
            {
                return (0, days);
            }
            var empIds = rows.Select(r => r.EmployeeId).Distinct().ToList();
            var current = await db.ShiftAssignment.AsNoTracking().Where(a => !a.IsDeleted && empIds.Contains(a.EmployeeId) && a.WorkDate >= from && a.WorkDate <= to).ToListAsync();
            var shifts = await db.Shift.AsNoTracking().ToDictionaryAsync(s => s.Id);
            int people = 0;
            foreach (var g in rows.GroupBy(r => r.EmployeeId))
            {
                var lines = g.GroupBy(r => r.WorkDate.Date).OrderBy(x => x.Key).Select(x =>
                {
                    days.Add((g.Key, x.Key));
                    var now = current.FirstOrDefault(a => a.EmployeeId == g.Key && a.WorkDate.Date == x.Key);
                    var old = shifts.GetValueOrDefault(x.First().ShiftId);
                    var cur = now != null ? shifts.GetValueOrDefault(now.ShiftId) : null;
                    return now == null ? $"{x.Key:dd/MM}: ca {old?.Code} → đã hủy ca"
                        : $"{x.Key:dd/MM}: ca {old?.Code} → {cur?.Code} {now.PlannedStart:HH:mm}–{now.PlannedEnd:HH:mm}";
                }).ToList();
                string key = string.Join(",", g.Select(r => r.Id.ToString("N")).OrderBy(x => x));
                string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)))[..12];
                people += await BusinessNotifier.SafeEnqueueAsync(_notifications, new NotificationRequest
                {
                    Template = NotificationCatalog.ShiftChanged, EmployeeIds = [g.Key],
                    Data = new { count = lines.Count, lines },
                    IdempotencyKey = $"SHIFT_CHANGED:{g.Key:N}:{hash}", CorrelationId = $"SHIFT:{g.Key:N}",
                }) > 0 ? 1 : 0;
                foreach (var r in g)
                {
                    r.Note = r.Note!.Replace(ChangedMark, NotifiedMark);
                }
            }
            await db.SaveChangesAsync();
            return (people, days);
        }

        // ================================================================ Nhắc lịch tuần sau (Thứ 6)

        /// <summary>Đúng thứ cấu hình (mặc định Thứ 6) và sau giờ nhắc: tuần sau còn ca nháp / thiếu người → 1 tin gộp cho trưởng đơn vị + nhân sự (1 lần / tuần).</summary>
        public async Task<int> RemindNextWeekAsync()
        {
            DateTime now = _clock.Now, today = _clock.Today;
            if (MenuService.MenuDayOf(today) != _hr.ScheduleRemindDay || !TimeSpan.TryParse(_hr.ScheduleRemindTime, out var at) || now.TimeOfDay < at)
            {
                return 0;
            }
            DateTime next = WeekStartOf(today).AddDays(7);
            var grid = await GetGrid(new ShiftGridRequest { WeekStart = next });
            var m = grid.Data;
            if (m?.WeekStatus == null || m.Kpi == null || (m.WeekStatus.DraftCount == 0 && m.Kpi.ShortSlots == 0 && m.WeekStatus.AssignedCount > 0))
            {
                return 0;
            }
            var db = _unitOfWork.DbContext;
            var shortUnits = m.UnitTotals.Where(t => t.Short).GroupBy(t => t.UnitName).Select(g => $"{g.Key}: thiếu {g.Count()} ca").Take(10).ToList();
            string text = $"🗓 Lịch ca tuần {HrRules.Day(next)} – {HrRules.Day(next.AddDays(6))}: " + (m.WeekStatus.AssignedCount == 0 ? "chưa phân ca nào" : $"{m.WeekStatus.DraftCount} ca chưa chốt, {m.Kpi.ShortSlots} ca thiếu người")
                + (shortUnits.Count > 0 ? "\n" + string.Join("\n", shortUnits.Select(x => "• " + x)) : string.Empty) + "\nMở màn Phân ca để hoàn thiện và Chốt & gửi lịch.";
            var managers = await db.OrganizationUnit.AsNoTracking().Where(o => !o.IsDeleted && o.ManagerId != null).Select(o => o.ManagerId!.Value).Distinct().ToListAsync();
            int n = 0;
            if (managers.Count > 0)
            {
                n += await BusinessNotifier.SafeEnqueueAsync(_notifications, new NotificationRequest
                {
                    Template = NotificationCatalog.ManualText, EmployeeIds = managers, Data = new { text },
                    IdempotencyKey = $"SHIFT_WEEK_REMIND:MGR:{next:yyyyMMdd}",
                });
            }
            n += await BusinessNotifier.ToMenusAsync(_notifications, db, NotificationCatalog.ManualText, ["#/hr-shift-assign"], new { text }, $"SHIFT_WEEK_REMIND:HR:{next:yyyyMMdd}");
            return n;
        }
    }
}
