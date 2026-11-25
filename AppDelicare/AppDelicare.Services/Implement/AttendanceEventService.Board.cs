using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.38 (D-119): Chấm công – tab Hôm nay (theo ca), Bảng công tháng (ma trận + Excel), Bổ sung giờ thực tế (quên vào / ra ca),
    /// xác nhận chấm ngoài bán kính, báo vắng. Sổ vẫn chỉ ghi thêm: bổ sung = thêm 1 dòng PROXY có giờ quá khứ; xác nhận = dấu "[ĐÃ XÁC NHẬN]" trong Note.
    /// </summary>
    public partial class AttendanceEventService
    {
        internal const string SupplementMark = "[BỔ SUNG]";
        internal const string ConfirmedMark = "[ĐÃ XÁC NHẬN]";

        internal const string ConfirmAction = "ConfirmValid";

        /// <summary>Lần chấm ngoài bán kính đã xác nhận (sổ chỉ ghi thêm – xác nhận ghi ở lịch sử thay đổi, không sửa dòng gốc).</summary>
        internal static async Task<HashSet<Guid>> ConfirmedIdsAsync(Data.Connection.DatabaseContext db, IReadOnlyCollection<Guid> eventIds)
            => eventIds.Count == 0 ? [] : (await db.AuditChange.AsNoTracking().Where(a => !a.IsDeleted && a.EntityName == nameof(AttendanceEvent) && a.Action == ConfirmAction && a.IdChange != null && eventIds.Contains(a.IdChange.Value))
                .Select(a => a.IdChange!.Value).ToListAsync()).ToHashSet();
        internal static bool IsSupplement(string? note) => note != null && note.Contains(SupplementMark, StringComparison.Ordinal);

        private string ReasonLabel(string? code)
            => _services.GetRequiredService<IOptions<ReferenceListsOptions>>().Value.Lists.TryGetValue("AttendanceSupplementReason", out var l)
               && l.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)) is { } i ? i.Label : code ?? string.Empty;

        private async Task<string?> ActorNameAsync()
        {
            Guid actor = _currentActor.RequireActor().UserId;
            return await _unitOfWork.DbContext.User.AsNoTracking().Where(u => u.Id == actor).Select(u => u.UserName).FirstOrDefaultAsync();
        }

        // ================================================================ Bổ sung giờ

        public async Task<IResult<AttendanceResultModel>> Supplement(AttendanceSupplementRequest request)
        {
            string? type = request.EventType?.Trim().ToUpperInvariant();
            if (type is not (AttendanceEventType.CheckIn or AttendanceEventType.CheckOut))
            {
                return Result<AttendanceResultModel>.Fail(ConstantHelper.Message.HrSupplementTypeInvalid);
            }
            if (string.IsNullOrWhiteSpace(request.ReasonCode))
            {
                return Result<AttendanceResultModel>.Fail(ConstantHelper.Message.HrSupplementReasonRequired);
            }
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime now = _clock.Now, today = _clock.Today;
                var a = _unitOfWork.ShiftAssignmentRepository.GetAll().FirstOrDefault(x => x.Id == request.ShiftAssignmentId && !x.IsDeleted);
                var employee = a == null ? null : await db.Employee.AsNoTracking().FirstOrDefaultAsync(e => e.Id == a.EmployeeId && !e.IsDeleted);
                if (a == null || employee == null || (request.EmployeeId != Guid.Empty && request.EmployeeId != a.EmployeeId))
                {
                    return Fail(ConstantHelper.Message.HrSupplementShiftNotFound);
                }
                if ((today - a.WorkDate.Date).TotalDays > _hr.SupplementMaxDays)
                {
                    return Fail(string.Format(ConstantHelper.Message.HrSupplementTooOld, _hr.SupplementMaxDays));
                }
                var shift = await db.Shift.AsNoTracking().FirstOrDefaultAsync(s => s.Id == a.ShiftId);
                var (start, end) = a.PlannedStart != null && a.PlannedEnd != null ? (a.PlannedStart.Value, a.PlannedEnd.Value)
                    : shift != null ? HrRules.PlannedWindow(a.WorkDate, shift) : (a.WorkDate.Date, a.WorkDate.Date.AddDays(1));
                DateTime lo = start.AddMinutes(-_hr.CheckInEarlyMinutes), hi = end.AddHours(_hr.SupplementWindowHours);
                DateTime at = request.EventAt;
                if (at < lo || at > hi || at > now)
                {
                    return Fail(string.Format(ConstantHelper.Message.HrSupplementOutOfWindow, lo.ToString("HH:mm dd/MM"), (hi < now ? hi : now).ToString("HH:mm dd/MM")));
                }
                var events = await db.AttendanceEvent.AsNoTracking().Where(e => !e.IsDeleted && e.ShiftAssignmentId == a.Id).ToListAsync();
                if (events.Any(e => e.EventType == type))
                {
                    return Fail(string.Format(ConstantHelper.Message.HrSupplementExists, AttendanceEventType.Label(type).ToLowerInvariant()));
                }
                var inEv = events.FirstOrDefault(e => e.EventType == AttendanceEventType.CheckIn);
                var outEv = events.FirstOrDefault(e => e.EventType == AttendanceEventType.CheckOut);
                if (type == AttendanceEventType.CheckOut && inEv != null && at <= inEv.EventAt)
                {
                    return Fail(ConstantHelper.Message.HrSupplementOutBeforeIn);
                }
                if (type == AttendanceEventType.CheckIn && outEv != null && at >= outEv.EventAt)
                {
                    return Fail(ConstantHelper.Message.HrSupplementOutBeforeIn);
                }
                int late = type == AttendanceEventType.CheckIn ? Minutes(at - start) : 0, early = type == AttendanceEventType.CheckOut ? Minutes(end - at) : 0;
                late = late > _hr.LateGraceMinutes ? late : 0;
                early = early > _hr.EarlyLeaveGraceMinutes ? early : 0;
                var notes = new List<string> { $"{SupplementMark} {ReasonLabel(request.ReasonCode)} – ghi bởi {await ActorNameAsync()}" };
                if (late > 0)
                {
                    notes.Add($"Đi muộn {late} phút");
                }
                if (early > 0)
                {
                    notes.Add($"Về sớm {early} phút");
                }
                if (!string.IsNullOrWhiteSpace(request.Note))
                {
                    notes.Add(request.Note.Trim());
                }
                string note = string.Join("; ", notes);
                var ev = new AttendanceEvent
                {
                    Id = Guid.NewGuid(), EmployeeId = a.EmployeeId, ShiftAssignmentId = a.Id, EventType = type, EventAt = at, IsValid = true, SourceChannel = AttendanceSource.Proxy,
                    Note = note.Length <= 1000 ? note : note[..1000],
                };
                _unitOfWork.AttendanceEventRepository.Add(ev);
                bool hasIn = inEv != null || type == AttendanceEventType.CheckIn, hasOut = outEv != null || type == AttendanceEventType.CheckOut;
                a.AssignmentStatus = hasIn && hasOut ? ShiftAssignmentStatus.Completed : hasIn ? ShiftAssignmentStatus.CheckedIn : a.AssignmentStatus;
                _unitOfWork.ShiftAssignmentRepository.Update(a);
                await _unitOfWork.AttendanceEventRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                var model = await ResultModelAsync(ev, employee, null, null, late, early);
                return Result<AttendanceResultModel>.Success(model, string.Format(ConstantHelper.Message.HrSupplementDone, AttendanceEventType.Label(type).ToLowerInvariant(), at.ToString("HH:mm dd/MM"), employee.FullName));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Supplement: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<AttendanceResultModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<AttendanceResultModel>.Fail(message);
            }
        }

        // ================================================================ Xác nhận ngoài bán kính / báo vắng

        public async Task<IResult<bool>> ConfirmValid(AttendanceConfirmValidRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Note))
            {
                return Result<bool>.Fail(ConstantHelper.Message.HrConfirmValidNoteRequired);
            }
            var db = _unitOfWork.DbContext;
            var ev = await db.AttendanceEvent.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.Id && !e.IsDeleted);
            if (ev == null)
            {
                return Result<bool>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
            }
            if (ev.IsValid != false)
            {
                return Result<bool>.Fail(ConstantHelper.Message.HrConfirmValidNotOutside);
            }
            if ((await ConfirmedIdsAsync(db, [ev.Id])).Count > 0)
            {
                return Result<bool>.Fail(ConstantHelper.Message.HrConfirmValidAlready);
            }
            var actor = _currentActor.RequireActor();
            db.AuditChange.Add(new AuditChange
            {
                Id = Guid.NewGuid(), IdChange = ev.Id, EntityName = nameof(AttendanceEvent), Action = ConfirmAction, CreatedDate = _clock.Now, CreatedBy = actor.UserId, IsDeleted = false,
                Changes = $"- {ConfirmedMark} {request.Note.Trim()} – bởi {await ActorNameAsync()} {ConstantHelper.Break}",
            });
            await db.SaveChangesAsync();
            return Result<bool>.Success(true, ConstantHelper.Message.HrConfirmValidDone);
        }

        public async Task<IResult<bool>> ReportAbsent(AttendanceReportAbsentRequest request)
        {
            var db = _unitOfWork.DbContext;
            var a = _unitOfWork.ShiftAssignmentRepository.GetAll().FirstOrDefault(x => x.Id == request.ShiftAssignmentId && !x.IsDeleted);
            if (a == null)
            {
                return Result<bool>.Fail(ConstantHelper.Message.HrSupplementShiftNotFound);
            }
            if (!ShiftAssignmentStatus.Open.Contains(a.AssignmentStatus) || await db.AttendanceEvent.AnyAsync(e => !e.IsDeleted && e.ShiftAssignmentId == a.Id))
            {
                return Result<bool>.Fail(ConstantHelper.Message.HrReportAbsentAttended);
            }
            if (a.WorkDate.Date > _clock.Today)
            {
                return Result<bool>.Fail(ConstantHelper.Message.HrReportAbsentFuture);
            }
            a.AssignmentStatus = ShiftAssignmentStatus.Absent;
            a.Note = HrRules.AppendNote(a.Note, $"Báo vắng {_clock.Now:dd/MM HH:mm} – {await ActorNameAsync()}" + (string.IsNullOrWhiteSpace(request.Note) ? string.Empty : ": " + request.Note.Trim()));
            _unitOfWork.ShiftAssignmentRepository.Update(a);
            await _unitOfWork.ShiftAssignmentRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            return Result<bool>.Success(true, ConstantHelper.Message.HrReportAbsentDone);
        }

        // ================================================================ Tab Hôm nay

        public async Task<IResult<AttendanceDayBoardModel>> GetDayBoard(DateTime? date, Guid? shiftId, Guid? organizationUnitId)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime now = _clock.Now, day = (date ?? _clock.Today).Date;
                var emps = await TimesheetEmployeesAsync(new Model.Request.Custom.TimesheetRequest { OrganizationUnitId = organizationUnitId });
                var empMap = emps.ToDictionary(e => e.Id);
                var empIds = emps.Select(e => e.Id).ToList();
                var units = await db.OrganizationUnit.AsNoTracking().ToDictionaryAsync(o => o.Id, o => o.Name);
                var rows = await db.ShiftAssignment.AsNoTracking().Where(a => !a.IsDeleted && empIds.Contains(a.EmployeeId) && a.WorkDate == day).ToListAsync();
                var details = await DetailsAsync(rows);
                var shifts = await db.Shift.AsNoTracking().Where(s => !s.IsDeleted).ToDictionaryAsync(s => s.Id);
                var rowIds = rows.Select(r => r.Id).ToList();
                var events = await db.AttendanceEvent.AsNoTracking().Where(e => !e.IsDeleted && e.ShiftAssignmentId != null && rowIds.Contains(e.ShiftAssignmentId.Value)).ToListAsync();
                var confirmed = await ConfirmedIdsAsync(db, [.. events.Where(e => e.IsValid == false).Select(e => e.Id)]);
                var m = new AttendanceDayBoardModel { Date = day };
                foreach (var g in details.GroupBy(d => d.ShiftId))
                {
                    DateTime? s = g.Min(x => x.PlannedStart), e = g.Max(x => x.PlannedEnd);
                    m.ListShift.Add(new AttendanceDayShiftModel
                    {
                        Id = g.Key, Code = shifts.GetValueOrDefault(g.Key)?.Code, Name = shifts.GetValueOrDefault(g.Key)?.Name, Start = s, End = e,
                        State = s > now ? "UPCOMING" : e < now ? "DONE" : "RUNNING", Assigned = g.Count(), Present = g.Count(x => x.CheckInAt != null),
                    });
                }
                m.ListShift = [.. m.ListShift.OrderBy(x => x.Start)];
                m.ShiftId = shiftId ?? (m.ListShift.FirstOrDefault(x => x.State == "RUNNING") ?? m.ListShift.FirstOrDefault(x => x.State == "UPCOMING") ?? m.ListShift.LastOrDefault())?.Id;
                var leaves = await HrRules.LeavesAsync(db, empIds, day, day, true);
                AttendancePersonModel P(Guid id) => new() { EmployeeId = id, Code = empMap.GetValueOrDefault(id)?.Code, FullName = empMap.GetValueOrDefault(id)?.FullName, Phone = empMap.GetValueOrDefault(id)?.Phone,
                    UnitName = empMap.GetValueOrDefault(id)?.OrganizationUnitId is { } u ? units.GetValueOrDefault(u) : null };
                foreach (var d in details.Where(x => x.ShiftId == m.ShiftId))
                {
                    if (leaves.Any(l => l.EmployeeId == d.EmployeeId))
                    {
                        continue;
                    }
                    var p = P(d.EmployeeId);
                    p.AssignmentId = d.AssignmentId; p.WorkDate = d.WorkDate; p.ShiftCode = d.ShiftCode; p.PlannedStart = d.PlannedStart; p.PlannedEnd = d.PlannedEnd;
                    p.CheckInAt = d.CheckInAt; p.CheckOutAt = d.CheckOutAt; p.LateMinutes = d.LateMinutes;
                    if (d.CheckInAt == null)
                    {
                        if (d.Status == ShiftAssignmentStatus.Absent)
                        {
                            continue;
                        }
                        p.OverdueMinutes = d.PlannedStart is { } ps && ps < now ? Minutes(now - ps) : 0;
                        m.ListNotCheckedIn.Add(p);
                    }
                    else
                    {
                        var inEv = events.Where(e => e.ShiftAssignmentId == d.AssignmentId && e.EventType == AttendanceEventType.CheckIn).OrderBy(e => e.EventAt).FirstOrDefault();
                        p.EventId = inEv?.Id;
                        p.Outside = inEv?.IsValid == false && !confirmed.Contains(inEv.Id);
                        p.DistanceMeters = inEv?.DistanceMeters;
                        m.ListCheckedIn.Add(p);
                    }
                }
                foreach (var l in leaves)
                {
                    var p = P(l.EmployeeId);
                    p.LeaveRequestNo = l.RequestNo;
                    p.LeaveTypeCode = l.LeaveTypeCode;
                    m.ListOnLeave.Add(p);
                }
                // Cần xử lý: 3 ngày gần nhất – thiếu ra ca; ngoài bán kính chưa xác nhận.
                DateTime from = _clock.Today.AddDays(-_hr.SupplementMaxDays);
                var recent = await db.ShiftAssignment.AsNoTracking().Where(a => !a.IsDeleted && empIds.Contains(a.EmployeeId) && a.WorkDate >= from && a.WorkDate <= _clock.Today).ToListAsync();
                var recentDetails = await DetailsAsync(recent);
                var recentIds = recent.Select(r => r.Id).ToList();
                var outside = await db.AttendanceEvent.AsNoTracking().Where(e => !e.IsDeleted && e.IsValid == false && e.ShiftAssignmentId != null && recentIds.Contains(e.ShiftAssignmentId.Value)).ToListAsync();
                foreach (var d in recentDetails.Where(x => x.MissingCheckOut))
                {
                    var p = P(d.EmployeeId);
                    p.Kind = "MISSING_OUT"; p.AssignmentId = d.AssignmentId; p.WorkDate = d.WorkDate; p.ShiftCode = d.ShiftCode; p.PlannedStart = d.PlannedStart; p.PlannedEnd = d.PlannedEnd; p.CheckInAt = d.CheckInAt;
                    m.ListNeedAction.Add(p);
                }
                var confirmedRecent = await ConfirmedIdsAsync(db, [.. outside.Select(e => e.Id)]);
                foreach (var e in outside.Where(e => !confirmedRecent.Contains(e.Id)))
                {
                    var d = recentDetails.FirstOrDefault(x => x.AssignmentId == e.ShiftAssignmentId);
                    var p = P(e.EmployeeId);
                    p.Kind = "OUTSIDE"; p.EventId = e.Id; p.AssignmentId = e.ShiftAssignmentId; p.WorkDate = d?.WorkDate; p.ShiftCode = d?.ShiftCode; p.DistanceMeters = e.DistanceMeters;
                    p.CheckInAt = e.EventType == AttendanceEventType.CheckIn ? e.EventAt : null; p.CheckOutAt = e.EventType == AttendanceEventType.CheckOut ? e.EventAt : null;
                    m.ListNeedAction.Add(p);
                }
                m.ListNotCheckedIn = [.. m.ListNotCheckedIn.OrderByDescending(x => x.OverdueMinutes).ThenBy(x => x.FullName)];
                m.ListCheckedIn = [.. m.ListCheckedIn.OrderBy(x => x.CheckInAt)];
                m.ListNeedAction = [.. m.ListNeedAction.OrderByDescending(x => x.WorkDate).ThenBy(x => x.FullName)];
                var sel = m.ListShift.FirstOrDefault(x => x.Id == m.ShiftId);
                m.Assigned = sel?.Assigned ?? 0;
                m.Present = m.ListCheckedIn.Count;
                m.NotCheckedInOverdue = m.ListNotCheckedIn.Count(x => x.OverdueMinutes > 0);
                m.LateMinutesTotal = m.ListCheckedIn.Sum(x => x.LateMinutes);
                m.NeedActionCount = m.ListNeedAction.Count;
                return Result<AttendanceDayBoardModel>.Success(m, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDayBoard: ", ex.ToString()));
                throw;
            }
        }

        // ================================================================ Bảng công tháng

        private static (DateTime From, DateTime To, string Month) MonthRange(string? month, DateTime today)
        {
            DateTime first = DateTime.TryParseExact((month ?? string.Empty) + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)
                ? d : new DateTime(today.Year, today.Month, 1);
            return (first, first.AddMonths(1).AddDays(-1), first.ToString("yyyy-MM"));
        }

        public async Task<IResult<AttendanceMonthGridModel>> GetMonthGrid(string? month, Guid? organizationUnitId)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime today = _clock.Today, now = _clock.Now;
                var (from, to, key) = MonthRange(month, today);
                var emps = await TimesheetEmployeesAsync(new Model.Request.Custom.TimesheetRequest { OrganizationUnitId = organizationUnitId });
                var empIds = emps.Select(e => e.Id).ToList();
                var rows = await db.ShiftAssignment.AsNoTracking().Where(a => !a.IsDeleted && empIds.Contains(a.EmployeeId) && a.WorkDate >= from && a.WorkDate <= to).ToListAsync();
                var details = await DetailsAsync(rows);
                var ids = rows.Select(r => r.Id).ToList();
                var events = await db.AttendanceEvent.AsNoTracking().Where(e => !e.IsDeleted && e.ShiftAssignmentId != null && ids.Contains(e.ShiftAssignmentId.Value)).ToListAsync();
                var leaves = await HrRules.LeavesAsync(db, empIds, from, to, true);
                var units = await db.OrganizationUnit.AsNoTracking().ToDictionaryAsync(o => o.Id, o => o.Name);
                var confirmed = await ConfirmedIdsAsync(db, [.. events.Where(e => e.IsValid == false).Select(e => e.Id)]);
                var m = new AttendanceMonthGridModel { Month = key, From = from, To = to };
                for (DateTime d = from; d <= to; d = d.AddDays(1))
                {
                    m.Days.Add(Key(d));
                }
                foreach (var e in emps)
                {
                    var mine = details.Where(x => x.EmployeeId == e.Id).ToList();
                    bool working = EmployeeStatus.IsWorking(e.Status);
                    if (!working && mine.Count == 0)
                    {
                        continue;
                    }
                    var row = new AttendanceMonthRowModel { EmployeeId = e.Id, Code = e.Code ?? e.EmployeeCode, FullName = e.FullName, UnitId = e.OrganizationUnitId, UnitName = e.OrganizationUnitId is { } u ? units.GetValueOrDefault(u) : null };
                    for (DateTime d = from; d <= to; d = d.AddDays(1))
                    {
                        var x = mine.FirstOrDefault(r => r.WorkDate.Date == d);
                        bool leave = leaves.Any(l => l.EmployeeId == e.Id && l.FromDate.Date <= d && l.ToDate.Date >= d);
                        var cell = new AttendanceMonthCellModel();
                        if (leave)
                        {
                            row.Leave++;
                        }
                        if (x == null)
                        {
                            cell.Symbol = leave ? "P" : "–";
                        }
                        else
                        {
                            var evs = events.Where(v => v.ShiftAssignmentId == x.AssignmentId).ToList();
                            var outside = evs.FirstOrDefault(v => v.IsValid == false && !confirmed.Contains(v.Id));
                            cell.AssignmentId = x.AssignmentId; cell.ShiftCode = x.ShiftCode; cell.CheckInAt = x.CheckInAt; cell.CheckOutAt = x.CheckOutAt;
                            cell.LateMinutes = x.LateMinutes; cell.EarlyLeaveMinutes = x.EarlyLeaveMinutes; cell.Supplemented = evs.Any(v => IsSupplement(v.Note)); cell.OutsideEventId = outside?.Id;
                            bool missing = x.MissingCheckOut || (x.CheckInAt == null && x.CheckOutAt != null);
                            cell.Symbol = x.CheckInAt == null && x.IsAbsent ? (leave ? "P" : "V")
                                : x.CheckInAt == null ? (x.PlannedStart is { } ps && ps.Date == today && x.PlannedEnd > now ? "…" : string.Empty)
                                : missing ? "T" : outside != null ? "!" : x.LateMinutes > 0 || x.EarlyLeaveMinutes > 0 ? "M" : "✓";
                            if (x.CheckInAt != null)
                            {
                                row.Work++;
                            }
                            if (x.LateMinutes > 0 || x.EarlyLeaveMinutes > 0)
                            {
                                row.Late++;
                            }
                            if (x.IsAbsent)
                            {
                                row.Absent++;
                            }
                            row.Hours += x.WorkedMinutes / 60m;
                            if (missing)
                            {
                                m.MissingTime++;
                            }
                            if (outside != null)
                            {
                                m.OutsideUnconfirmed++;
                            }
                            if (x.IsAbsent && !leave)
                            {
                                m.AbsentNoLeave++;
                            }
                        }
                        row.Cells[Key(d)] = cell;
                    }
                    row.Hours = Math.Round(row.Hours, 2);
                    m.ListRow.Add(row);
                }
                m.ListRow = [.. m.ListRow.OrderBy(r => r.UnitName).ThenBy(r => r.FullName)];
                return Result<AttendanceMonthGridModel>.Success(m, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMonthGrid: ", ex.ToString()));
                throw;
            }
        }

        private static string Key(DateTime d) => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        public async Task<IResult<FileContentModel>> ExportTimesheet(string? month, Guid? organizationUnitId)
        {
            var grid = await GetMonthGrid(month, organizationUnitId);
            if (grid.Data == null)
            {
                return Result<FileContentModel>.Fail(grid.ResponseMessage ?? ConstantHelper.Message.InternalServer);
            }
            var m = grid.Data;
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Bang cong " + m.Month);
            int days = m.Days.Count, last = 3 + days + 5;
            ws.Range(1, 1, 1, last).Merge().Value = $"BẢNG CÔNG THÁNG {m.From:MM/yyyy}";
            ws.Range(1, 1, 1, last).Style.Font.SetBold().Font.SetFontSize(14).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Range(2, 1, 2, last).Merge().Value = "Ký hiệu: ✓ đủ · M muộn / về sớm · V vắng · P phép · T thiếu ra / vào ca · ! ngoài bán kính chưa xác nhận · – không có ca";
            string[] head = ["Mã NV", "Họ tên", "Tổ / đơn vị"];
            for (int i = 0; i < head.Length; i++)
            {
                ws.Cell(4, i + 1).Value = head[i];
            }
            for (int i = 0; i < days; i++)
            {
                DateTime d = m.From.AddDays(i);
                ws.Cell(4, 4 + i).Value = d.Day;
                if (d.DayOfWeek == DayOfWeek.Sunday)
                {
                    ws.Column(4 + i).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F2F4F1"));
                }
            }
            string[] tot = ["Công", "Giờ", "Muộn", "Vắng", "Phép"];
            for (int i = 0; i < tot.Length; i++)
            {
                ws.Cell(4, 4 + days + i).Value = tot[i];
            }
            ws.Range(4, 1, 4, last).Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml("#91AF42")).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            int r = 4;
            foreach (var row in m.ListRow)
            {
                r++;
                ws.Cell(r, 1).Value = row.Code ?? string.Empty;
                ws.Cell(r, 2).Value = row.FullName ?? string.Empty;
                ws.Cell(r, 3).Value = row.UnitName ?? string.Empty;
                for (int i = 0; i < days; i++)
                {
                    ws.Cell(r, 4 + i).Value = row.Cells.TryGetValue(m.Days[i], out var c) ? c.Symbol : string.Empty;
                }
                ws.Cell(r, 4 + days).Value = row.Work;
                ws.Cell(r, 5 + days).Value = row.Hours;
                ws.Cell(r, 6 + days).Value = row.Late;
                ws.Cell(r, 7 + days).Value = row.Absent;
                ws.Cell(r, 8 + days).Value = row.Leave;
            }
            ws.Range(4, 4, r, last).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Range(4, 1, Math.Max(r, 4), last).Style.Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            ws.Columns(1, 3).AdjustToContents();
            ws.Columns(4, 3 + days).Width = 4;
            ws.SheetView.FreezeRows(4);
            ws.SheetView.FreezeColumns(3);
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return Result<FileContentModel>.Success(new FileContentModel
            {
                Content = ms.ToArray(), ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", FileName = $"Bang_cong_{m.Month}.xlsx",
            }, ConstantHelper.Message.GetAllSuccessfully);
        }
    }
}
