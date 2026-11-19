// Sinh bởi tools/AppDelicare.CodeGen (T05), nay là file của dev (BE2.6: lưới ca, phân ca, phân ca hàng loạt, xác nhận, đánh vắng) – CodeGen không ghi đè.
#nullable enable
using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Globalization;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class ShiftAssignmentService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper, IReferenceGuard referenceGuard
        , IClock clock, ICurrentActor currentActor, IOptions<BusinessRulesOptions> rules, INotificationService notifications) : IShiftAssignmentService
    {
        private readonly INotificationService _notifications = notifications;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IReferenceGuard _referenceGuard = referenceGuard;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly HrRulesOptions _hr = rules.Value.Hr;

        public async Task<IResults<ShiftAssignmentViewModel>> GetAll(ShiftAssignmentSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@EmployeeId", request.EmployeeId);
                dynamicParameters.Add("@ShiftId", request.ShiftId);
                dynamicParameters.Add("@AssignmentStatus", request.AssignmentStatus);
                dynamicParameters.Add("@FromDate", request.FromDate?.Date);
                dynamicParameters.Add("@ToDate", request.ToDate?.Date);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<ShiftAssignmentViewModel>("uspShiftAssignment_GetAll", dynamicParameters)).ToList();
                int i = 0;
                list.ForEach(x =>
                {
                    i++;
                    x.STT = (request.Page - 1) * request.PageSize + i;
                });
                return new Results<ShiftAssignmentViewModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.FirstOrDefault()?.TotalRow ?? 0
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAll: ", ex.ToString()));
                throw;
            }
        }

        public IResults<ShiftAssignment> GetAllData()
        {
            try
            {
                var list = _unitOfWork.ShiftAssignmentRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<ShiftAssignment>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.Count
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAllData: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<bool>> Save(ShiftAssignmentRequest request, string typeSave)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var result = string.Empty;
                switch (typeSave)
                {
                    case ConstantHelper.TypeSave.Insert:
                        result = await Insert(request, typeSave);
                        break;
                    case ConstantHelper.TypeSave.Update:
                        result = await Update(request, typeSave);
                        break;
                    case ConstantHelper.TypeSave.Delete:
                        result = await Delete(request.Id, typeSave);
                        break;
                    default:
                        result = "Loại thao tác lưu dữ liệu không hợp lệ.";
                        break;
                }
                _unitOfWork.CommitTransaction();
                if (!string.IsNullOrEmpty(result))
                {
                    return new Result<bool>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = result,
                        Data = false
                    };
                }
                return new Result<bool>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = typeSave == ConstantHelper.TypeSave.Delete ? ConstantHelper.Message.DeleteSuccess : ConstantHelper.Message.SaveSuccess,
                    Data = true
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Save: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>BE2.6: phân ca chỉ qua <c>ShiftAssignment/Assign</c> / <c>BulkAssign</c> (kiểm nghỉ phép, chấm công, đổi ca, UNIQUE kể cả dòng đã xóa mềm).</summary>
        private Task<string> Insert(ShiftAssignmentRequest request, string typeSave) => Task.FromResult(ConstantHelper.Message.HrAssignmentCrudBlocked);

        private Task<string> Update(ShiftAssignmentRequest request, string typeSave) => Task.FromResult(ConstantHelper.Message.HrAssignmentCrudBlocked);

        /// <summary>BE2.6: bỏ phân ca = <c>Assign</c> với ShiftId trống (kiểm chấm công / đổi ca).</summary>
        private Task<string> Delete(Guid? id, string typeSave) => Task.FromResult(ConstantHelper.Message.HrAssignmentCrudBlocked);

        public async Task<IResult<ShiftAssignment>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.ShiftAssignmentRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<ShiftAssignment>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<ShiftAssignment>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    Data = data,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetById: ", ex.ToString()));
                throw;
            }
        }

        // ============================================================ BE2.6 – phân ca (spec 11.1, Catalog 201–204)
        private static string Key(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        internal static bool ShiftActive(Shift s) => !s.IsDeleted && (string.IsNullOrEmpty(s.Status) || s.Status == "Active");

        /// <summary>Nhân viên đang làm (chưa xóa, chưa nghỉ việc) trong phạm vi đơn vị (gồm đơn vị con).</summary>
        private async Task<List<Employee>> EmployeesAsync(Guid? organizationUnitId, string? searchText, IReadOnlyCollection<Guid>? ids = null)
        {
            var db = _unitOfWork.DbContext;
            var scope = await HrRules.OrganizationScopeAsync(db, organizationUnitId);
            var query = db.Employee.AsNoTracking().Where(e => !e.IsDeleted);
            if (ids != null)
            {
                query = query.Where(e => ids.Contains(e.Id));
            }
            if (scope != null)
            {
                query = query.Where(e => e.OrganizationUnitId != null && scope.Contains(e.OrganizationUnitId.Value));
            }
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                string s = searchText.Trim();
                query = query.Where(e => (e.FullName != null && e.FullName.Contains(s)) || (e.Code != null && e.Code.Contains(s)) || (e.EmployeeCode != null && e.EmployeeCode.Contains(s)));
            }
            return [.. (await query.ToListAsync()).Where(e => EmployeeStatus.IsWorking(e.Status)).OrderBy(e => e.FullName)];
        }

        /// <summary>
        /// Spec 11.1 GET shift-grid (Catalog 201): nhân viên × ngày (mặc định 7 ngày từ hôm nay). Ô = ca được phân (mã ca, trạng thái), <c>onLeave</c> = có đơn nghỉ Đã duyệt bao
        /// ngày đó (<c>leavePending</c> = đơn Chờ duyệt), <c>hasAttendance</c>, <c>pendingSwap</c>. <c>kpiToday</c>: theo ca – số được phân / số đã vào ca hôm nay.
        /// </summary>
        public async Task<IResult<ShiftGridModel>> GetGrid(ShiftGridRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime from = (request.FromDate is { } f && f != default ? f : _clock.Today).Date;
                int days = Math.Clamp(request.Days ?? _hr.GridDays, 1, 31);
                DateTime to = from.AddDays(days - 1);
                var employees = await EmployeesAsync(request.OrganizationUnitId, request.SearchText);
                var empIds = employees.Select(e => e.Id).ToList();
                DateTime today = _clock.Today;
                DateTime lo = from < today ? from : today, hi = to > today ? to : today;
                var assignments = await db.ShiftAssignment.AsNoTracking()
                    .Where(a => !a.IsDeleted && empIds.Contains(a.EmployeeId) && a.WorkDate >= lo && a.WorkDate <= hi).ToListAsync();
                var shifts = await db.Shift.AsNoTracking().Where(s => !s.IsDeleted).ToListAsync();
                var shiftMap = shifts.ToDictionary(s => s.Id);
                var leaves = await HrRules.LeavesAsync(db, empIds, from, to, false);
                var ids = assignments.Select(a => a.Id).ToList();
                var attended = await HrRules.AttendedAssignmentsAsync(db, ids);
                var swaps = await HrRules.PendingSwapAssignmentsAsync(db, ids);
                var positionIds = employees.Where(e => e.PositionId != null).Select(e => e.PositionId!.Value).Distinct().ToList();
                var ouIds = employees.Where(e => e.OrganizationUnitId != null).Select(e => e.OrganizationUnitId!.Value).Distinct().ToList();
                var positions = await db.Position.AsNoTracking().Where(p => positionIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name);
                var ous = await db.OrganizationUnit.AsNoTracking().Where(o => ouIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Name);
                var model = new ShiftGridModel
                {
                    FromDate = from,
                    ToDate = to,
                    Days = [.. Enumerable.Range(0, days).Select(i => Key(from.AddDays(i)))],
                    Shifts = [.. shifts.Where(ShiftActive).OrderBy(s => s.StartTime).Select(s => new ShiftGridShiftModel { Id = s.Id, Code = s.Code, Name = s.Name, StartTime = s.StartTime, EndTime = s.EndTime })],
                };
                foreach (var e in employees)
                {
                    var row = new ShiftGridEmployeeModel
                    {
                        Id = e.Id, Code = e.Code ?? e.EmployeeCode, FullName = e.FullName, PositionId = e.PositionId,
                        PositionName = e.PositionId is { } p ? positions.GetValueOrDefault(p) : null,
                        OrganizationUnitId = e.OrganizationUnitId, OrganizationUnitName = e.OrganizationUnitId is { } o ? ous.GetValueOrDefault(o) : null,
                    };
                    for (int i = 0; i < days; i++)
                    {
                        DateTime d = from.AddDays(i);
                        var a = assignments.Where(x => x.EmployeeId == e.Id && x.WorkDate.Date == d).OrderBy(x => x.PlannedStart).FirstOrDefault();
                        var leave = leaves.Where(l => l.EmployeeId == e.Id && l.FromDate.Date <= d && l.ToDate.Date >= d)
                            .OrderBy(l => l.RequestStatus == LeaveRequestStatus.Approved ? 0 : 1).FirstOrDefault();
                        var shift = a != null ? shiftMap.GetValueOrDefault(a.ShiftId) : null;
                        row.Cells[Key(d)] = new ShiftGridCellModel
                        {
                            AssignmentId = a?.Id, ShiftId = a?.ShiftId, ShiftCode = shift?.Code, ShiftName = shift?.Name,
                            PlannedStart = a?.PlannedStart, PlannedEnd = a?.PlannedEnd,
                            Status = a?.AssignmentStatus, StatusName = a == null ? null : ShiftAssignmentStatus.Label(a.AssignmentStatus),
                            OnLeave = leave?.RequestStatus == LeaveRequestStatus.Approved,
                            LeavePending = leave?.RequestStatus == LeaveRequestStatus.Pending,
                            LeaveRequestId = leave?.Id, LeaveRequestNo = leave?.RequestNo,
                            HasAttendance = a != null && attended.Contains(a.Id),
                            PendingSwap = a != null && swaps.Contains(a.Id),
                        };
                    }
                    model.Employees.Add(row);
                }
                var todayRows = assignments.Where(a => a.WorkDate.Date == today).ToList();
                model.KpiToday = [.. model.Shifts.Select(s => new ShiftGridKpiModel
                {
                    ShiftId = s.Id, ShiftCode = s.Code, ShiftName = s.Name,
                    Assigned = todayRows.Count(a => a.ShiftId == s.Id),
                    CheckedIn = todayRows.Count(a => a.ShiftId == s.Id && a.AssignmentStatus is ShiftAssignmentStatus.CheckedIn or ShiftAssignmentStatus.Completed),
                })];
                return Result<ShiftGridModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetGrid: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Lỗi nếu ca không đổi / bỏ được: đã chấm công hoặc đã qua trạng thái Xác nhận, hoặc đang trong đơn đổi ca chờ duyệt.</summary>
        private static string? LockedReason(ShiftAssignment a, Shift? shift, Employee e, HashSet<Guid> attended, HashSet<Guid> swaps)
        {
            if (attended.Contains(a.Id) || !ShiftAssignmentStatus.Open.Contains(a.AssignmentStatus))
            {
                return string.Format(ConstantHelper.Message.HrAssignmentAttended, shift?.Code, HrRules.Day(a.WorkDate), e.FullName, ShiftAssignmentStatus.Label(a.AssignmentStatus));
            }
            if (swaps.Contains(a.Id))
            {
                return string.Format(ConstantHelper.Message.HrAssignmentPendingSwap, shift?.Code, HrRules.Day(a.WorkDate), e.FullName);
            }
            return null;
        }

        /// <summary>Xóa mềm tay (KHÔNG dùng CommitAsync(Delete) – lệnh đó xóa mềm mọi bản ghi đang sửa trong cùng DbContext).</summary>
        internal static void SoftDelete(ShiftAssignment a, string note)
        {
            a.IsDeleted = true;
            a.Note = HrRules.AppendNote(a.Note, note);
        }

        /// <summary>
        /// Ghi 1 ca (nhân viên, ca, ngày) – UNIQUE (EmployeeId, ShiftId, WorkDate) tính cả dòng đã xóa mềm → có dòng đã xóa thì dùng lại (bỏ xóa mềm), không có thì thêm mới.
        /// Không lưu (caller commit).
        /// </summary>
        private ShiftAssignment Place(Guid employeeId, Shift shift, DateTime workDate, DateTime start, DateTime end, string? note, Guid actor)
        {
            DateTime day = workDate.Date;
            var row = _unitOfWork.ShiftAssignmentRepository.GetAll().FirstOrDefault(x => x.EmployeeId == employeeId && x.ShiftId == shift.Id && x.WorkDate == day);
            if (row == null)
            {
                row = new ShiftAssignment { Id = Guid.NewGuid(), EmployeeId = employeeId, ShiftId = shift.Id, WorkDate = day };
                _unitOfWork.ShiftAssignmentRepository.Add(row);
            }
            else
            {
                row.IsDeleted = false;
                _unitOfWork.ShiftAssignmentRepository.Update(row);
            }
            row.PlannedStart = start;
            row.PlannedEnd = end;
            row.AssignmentStatus = ShiftAssignmentStatus.Planned;
            row.AssignedBy = actor;
            row.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            return row;
        }

        /// <summary>
        /// Spec 11.1 PUT shift-assignments (Catalog 202) – upsert theo (nhân viên, ngày), 1 transaction. ShiftId trống = bỏ phân ca (xóa mềm). Chặn: ngày đã qua, nhân viên
        /// nghỉ việc / có đơn nghỉ Đã duyệt bao ngày đó, ca của ngày đã có chấm công / đã vào ca (dùng đơn đổi ca), ca đang trong đơn đổi ca chờ duyệt.
        /// PlannedStart / End theo giờ ca (ca qua đêm → sáng hôm sau) hoặc giờ gửi lên; trạng thái Kế hoạch; AssignedBy = người đăng nhập. 1 nhân viên 1 ca / ngày (PROPOSED – D-55 d).
        /// </summary>
        public async Task<IResult<ShiftAssignResultModel>> Assign(ShiftAssignRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                Guid actor = _currentActor.RequireActor().UserId;
                var employee = await db.Employee.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.EmployeeId && !e.IsDeleted);
                if (employee == null || !EmployeeStatus.IsWorking(employee.Status))
                {
                    return Fail(ConstantHelper.Message.HrEmployeeNotFound);
                }
                if (request.WorkDate == default)
                {
                    return Fail(ConstantHelper.Message.ModelStateMessage);
                }
                DateTime date = request.WorkDate.Date;
                if (date < _clock.Today)
                {
                    return Fail(string.Format(ConstantHelper.Message.HrWorkDatePast, HrRules.Day(date)));
                }
                var existing = _unitOfWork.ShiftAssignmentRepository.GetAll().Where(a => a.EmployeeId == employee.Id && a.WorkDate == date && !a.IsDeleted).ToList();
                var ids = existing.Select(a => a.Id).ToList();
                var attended = await HrRules.AttendedAssignmentsAsync(db, ids);
                var swaps = await HrRules.PendingSwapAssignmentsAsync(db, ids);
                var shifts = await db.Shift.AsNoTracking().Where(s => !s.IsDeleted).ToDictionaryAsync(s => s.Id);
                var result = new ShiftAssignResultModel();
                if (request.ShiftId is not { } shiftId || shiftId == Guid.Empty)
                {
                    if (existing.Count == 0)
                    {
                        result.Action = "Unchanged";
                        return Ok(result, string.Format(ConstantHelper.Message.HrAssignRemoved, HrRules.Day(date), employee.FullName));
                    }
                    foreach (var a in existing)
                    {
                        if (LockedReason(a, shifts.GetValueOrDefault(a.ShiftId), employee, attended, swaps) is { } locked)
                        {
                            return Fail(locked);
                        }
                    }
                    foreach (var a in existing)
                    {
                        SoftDelete(a, $"Bỏ phân ca {_clock.Now:dd/MM/yyyy HH:mm}");
                        _unitOfWork.ShiftAssignmentRepository.Update(a);
                    }
                    await _unitOfWork.ShiftAssignmentRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                    _unitOfWork.CommitTransaction();
                    result.Action = "Removed";
                    result.Assignment = (await HrRules.ModelsAsync(db, existing)).FirstOrDefault();
                    return Ok(result, string.Format(ConstantHelper.Message.HrAssignRemoved, HrRules.Day(date), employee.FullName));
                }
                if (!shifts.TryGetValue(shiftId, out var shift) || !ShiftActive(shift))
                {
                    return Fail(ConstantHelper.Message.HrShiftNotFound);
                }
                var leave = (await HrRules.LeavesAsync(db, [employee.Id], date, date, true)).FirstOrDefault();
                if (leave != null)
                {
                    return Fail(string.Format(ConstantHelper.Message.HrAssignmentOnLeave, employee.FullName, HrRules.Day(date), leave.RequestNo));
                }
                var (start, end) = HrRules.PlannedWindow(date, shift);
                if (request.PlannedStart != null || request.PlannedEnd != null)
                {
                    start = request.PlannedStart ?? start;
                    end = request.PlannedEnd ?? end;
                    if (end <= start || start < date || start >= date.AddDays(1) || end > date.AddDays(2))
                    {
                        return Fail(ConstantHelper.Message.HrPlannedTimeInvalid);
                    }
                }
                var same = existing.FirstOrDefault(a => a.ShiftId == shift.Id);
                foreach (var a in existing)
                {
                    bool touches = a != same || a.PlannedStart != start || a.PlannedEnd != end;
                    if (touches && LockedReason(a, shifts.GetValueOrDefault(a.ShiftId), employee, attended, swaps) is { } locked)
                    {
                        return Fail(locked);
                    }
                }
                ShiftAssignment row;
                if (same != null)
                {
                    row = same;
                    bool changed = row.PlannedStart != start || row.PlannedEnd != end;
                    row.PlannedStart = start;
                    row.PlannedEnd = end;
                    row.AssignedBy = actor;
                    if (request.Note != null)
                    {
                        row.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
                    }
                    if (changed && row.AssignmentStatus == ShiftAssignmentStatus.Confirmed)
                    {
                        row.AssignmentStatus = ShiftAssignmentStatus.Planned;
                        result.ListWarning.Add(ConstantHelper.Message.HrAssignmentReconfirm);
                    }
                    _unitOfWork.ShiftAssignmentRepository.Update(row);
                    foreach (var a in existing.Where(a => a != same))
                    {
                        SoftDelete(a, $"Đổi sang ca {shift.Code} {_clock.Now:dd/MM/yyyy HH:mm}");
                        _unitOfWork.ShiftAssignmentRepository.Update(a);
                    }
                    result.Action = "Updated";
                }
                else
                {
                    foreach (var a in existing)
                    {
                        SoftDelete(a, $"Đổi sang ca {shift.Code} {_clock.Now:dd/MM/yyyy HH:mm}");
                        _unitOfWork.ShiftAssignmentRepository.Update(a);
                    }
                    row = Place(employee.Id, shift, date, start, end, request.Note, actor);
                    result.Action = existing.Count > 0 ? "Updated" : "Created";
                }
                if ((await HrRules.LeavesAsync(db, [employee.Id], date, date, false)).FirstOrDefault(l => l.RequestStatus == LeaveRequestStatus.Pending) is { } pending)
                {
                    result.ListWarning.Add(string.Format(ConstantHelper.Message.HrAssignmentLeavePending, employee.FullName, pending.RequestNo));
                }
                await _unitOfWork.ShiftAssignmentRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                result.Assignment = (await HrRules.ModelsAsync(db, [row])).FirstOrDefault();
                return Ok(result, string.Format(ConstantHelper.Message.HrAssignSaved, shift.Code, HrRules.Day(date), employee.FullName));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Assign: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<ShiftAssignResultModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<ShiftAssignResultModel>.Fail(message);
            }

            static IResult<ShiftAssignResultModel> Ok(ShiftAssignResultModel data, string message)
                => Result<ShiftAssignResultModel>.Success(data, message);
        }

        /// <summary>
        /// Spec 11.1 POST bulk (Catalog 203): phân 1 ca cho nhiều nhân viên × khoảng ngày (≤ <c>Hr:BulkAssignMaxDays</c>), bỏ các thứ trong <c>SkipWeekdays</c> (1 = Thứ 2 … 7 = CN).
        /// Không ghi đè: bỏ qua ô đã có ca, ngày nghỉ phép (Đã duyệt), nhân viên nghỉ việc; trả { created, skipped, listSkipped }. 1 transaction.
        /// </summary>
        public async Task<IResult<ShiftBulkAssignResultModel>> BulkAssign(ShiftBulkAssignRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                Guid actor = _currentActor.RequireActor().UserId;
                var shift = await db.Shift.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.ShiftId && !s.IsDeleted);
                if (shift == null || !ShiftActive(shift))
                {
                    return Fail(ConstantHelper.Message.HrShiftNotFound);
                }
                DateTime from = request.FromDate.Date, to = request.ToDate.Date;
                if (request.FromDate == default || request.ToDate == default || to < from || (to - from).TotalDays + 1 > _hr.BulkAssignMaxDays)
                {
                    return Fail(string.Format(ConstantHelper.Message.HrDateRangeInvalid, _hr.BulkAssignMaxDays));
                }
                if (from < _clock.Today)
                {
                    return Fail(string.Format(ConstantHelper.Message.HrWorkDatePast, HrRules.Day(from)));
                }
                var wanted = request.EmployeeIds.Where(x => x != Guid.Empty).Distinct().ToList();
                if (wanted.Count == 0 && request.OrganizationUnitId == null)
                {
                    return Fail(ConstantHelper.Message.HrNoEmployee);
                }
                var employees = await EmployeesAsync(wanted.Count == 0 ? request.OrganizationUnitId : null, null, wanted.Count == 0 ? null : wanted);
                if (employees.Count == 0)
                {
                    return Fail(ConstantHelper.Message.HrNoEmployee);
                }
                var result = new ShiftBulkAssignResultModel();
                foreach (var missing in wanted.Where(id => employees.All(e => e.Id != id)))
                {
                    result.ListSkipped.Add(new ShiftBulkSkippedModel { EmployeeId = missing, WorkDate = from, Reason = ConstantHelper.Message.HrEmployeeNotFound });
                }
                var empIds = employees.Select(e => e.Id).ToList();
                var existing = await db.ShiftAssignment.AsNoTracking().Where(a => !a.IsDeleted && empIds.Contains(a.EmployeeId) && a.WorkDate >= from && a.WorkDate <= to).ToListAsync();
                var shiftCodes = await db.Shift.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Code);
                var leaves = await HrRules.LeavesAsync(db, empIds, from, to, true);
                var skip = request.SkipWeekdays.ToHashSet();
                foreach (var e in employees)
                {
                    for (DateTime d = from; d <= to; d = d.AddDays(1))
                    {
                        if (skip.Contains(MenuService.MenuDayOf(d)))
                        {
                            continue;
                        }
                        if (existing.FirstOrDefault(a => a.EmployeeId == e.Id && a.WorkDate.Date == d) is { } has)
                        {
                            result.ListSkipped.Add(new ShiftBulkSkippedModel { EmployeeId = e.Id, EmployeeName = e.FullName, WorkDate = d, Reason = string.Format(ConstantHelper.Message.HrBulkSkipAssigned, shiftCodes.GetValueOrDefault(has.ShiftId)) });
                            continue;
                        }
                        if (leaves.FirstOrDefault(l => l.EmployeeId == e.Id && l.FromDate.Date <= d && l.ToDate.Date >= d) is { } leave)
                        {
                            result.ListSkipped.Add(new ShiftBulkSkippedModel { EmployeeId = e.Id, EmployeeName = e.FullName, WorkDate = d, Reason = string.Format(ConstantHelper.Message.HrBulkSkipLeave, leave.RequestNo) });
                            continue;
                        }
                        var (start, end) = HrRules.PlannedWindow(d, shift);
                        Place(e.Id, shift, d, start, end, request.Note, actor);
                        result.Created++;
                    }
                }
                result.Skipped = result.ListSkipped.Count;
                await _unitOfWork.ShiftAssignmentRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<ShiftBulkAssignResultModel>.Success(result, string.Format(ConstantHelper.Message.HrBulkDone, result.Created, result.Skipped));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " BulkAssign: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<ShiftBulkAssignResultModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<ShiftBulkAssignResultModel>.Fail(message);
            }
        }

        /// <summary>Ca còn hiệu lực trong khoảng ngày của nhân viên thuộc đơn vị (+ ca).</summary>
        private async Task<List<ShiftAssignment>> RowsAsync(DateTime from, DateTime to, Guid? organizationUnitId, Guid? shiftId)
        {
            var db = _unitOfWork.DbContext;
            var rows = _unitOfWork.ShiftAssignmentRepository.GetAll().Where(a => !a.IsDeleted && a.WorkDate >= from && a.WorkDate <= to && (shiftId == null || a.ShiftId == shiftId)).ToList();
            var scope = await HrRules.OrganizationScopeAsync(db, organizationUnitId);
            if (scope != null)
            {
                var empIds = rows.Select(r => r.EmployeeId).Distinct().ToList();
                var inScope = (await db.Employee.AsNoTracking().Where(e => empIds.Contains(e.Id) && e.OrganizationUnitId != null).Select(e => new { e.Id, e.OrganizationUnitId }).ToListAsync())
                    .Where(e => scope.Contains(e.OrganizationUnitId!.Value)).Select(e => e.Id).ToHashSet();
                rows = [.. rows.Where(r => inScope.Contains(r.EmployeeId))];
            }
            return rows;
        }

        /// <summary>
        /// Spec 11.1 POST confirm (Catalog 204): ca Kế hoạch của ngày (đến <c>ToDate</c>, theo đơn vị / ca) → Xác nhận. BOT-1.7: "gửi Telegram cho nhân viên" –
        /// mỗi nhân viên 1 tin SHIFT_CONFIRMED gom các ca vừa xác nhận (người chưa liên kết bot bỏ qua); <c>notificationSent</c> = đã xếp ít nhất 1 tin.
        /// </summary>
        public async Task<IResult<ShiftConfirmResultModel>> Confirm(ShiftConfirmRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                DateTime from = request.WorkDate.Date, to = (request.ToDate ?? request.WorkDate).Date;
                if (request.WorkDate == default || to < from || (to - from).TotalDays + 1 > 31)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<ShiftConfirmResultModel>.Fail(string.Format(ConstantHelper.Message.HrDateRangeInvalid, 31));
                }
                var rows = await RowsAsync(from, to, request.OrganizationUnitId, request.ShiftId);
                var result = new ShiftConfirmResultModel { FromDate = from, ToDate = to };
                var confirmed = new List<ShiftAssignment>();
                foreach (var row in rows)
                {
                    if (row.AssignmentStatus != ShiftAssignmentStatus.Planned)
                    {
                        result.AlreadyConfirmed++;
                        continue;
                    }
                    row.AssignmentStatus = ShiftAssignmentStatus.Confirmed;
                    _unitOfWork.ShiftAssignmentRepository.Update(row);
                    result.Confirmed++;
                    confirmed.Add(row);
                }
                await _unitOfWork.ShiftAssignmentRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                result.NotificationSent = await NotifyConfirmedAsync(confirmed) > 0;
                _unitOfWork.CommitTransaction();
                return Result<ShiftConfirmResultModel>.Success(result, string.Format(ConstantHelper.Message.HrConfirmDone, result.Confirmed, result.NotificationSent ? ConstantHelper.Message.HrConfirmNotified : string.Empty));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Confirm: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>
        /// Spec 11.2 "job cuối ngày đánh Absent" – D-45 không job: bấm tay. Ca Kế hoạch / Xác nhận của ngày đã hết giờ kế hoạch mà không có chấm công → Vắng.
        /// Bảng công tự tính vắng cho ca hết giờ chưa vào ca, nên bước này chỉ để chốt trạng thái.
        /// </summary>
        public async Task<IResult<ShiftMarkAbsentResultModel>> MarkAbsent(ShiftMarkAbsentRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime day = request.WorkDate.Date, now = _clock.Now;
                var rows = (await RowsAsync(day, day, request.OrganizationUnitId, null))
                    .Where(r => ShiftAssignmentStatus.Open.Contains(r.AssignmentStatus) && r.PlannedEnd != null && r.PlannedEnd < now).ToList();
                var attended = await HrRules.AttendedAssignmentsAsync(db, rows.Select(r => r.Id).ToList());
                var marked = rows.Where(r => !attended.Contains(r.Id)).ToList();
                foreach (var row in marked)
                {
                    row.AssignmentStatus = ShiftAssignmentStatus.Absent;
                    _unitOfWork.ShiftAssignmentRepository.Update(row);
                }
                await _unitOfWork.ShiftAssignmentRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                var result = new ShiftMarkAbsentResultModel { Marked = marked.Count, ListAssignment = await HrRules.ModelsAsync(db, marked) };
                return Result<ShiftMarkAbsentResultModel>.Success(result, string.Format(ConstantHelper.Message.HrMarkAbsentDone, marked.Count));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " MarkAbsent: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>BOT-1.5a: lịch ca của chính tài khoản (nhân viên nối theo HrRules.ResolveEmployeeAsync – định danh / mã = tên đăng nhập / số điện thoại).</summary>
        public async Task<IResults<BotEmployeeShiftModel>> GetMine(int days)
        {
            var employee = await HrRules.ResolveEmployeeAsync(_unitOfWork.DbContext, _currentActor.RequireActor().UserId);
            if (employee == null)
            {
                return Results<BotEmployeeShiftModel>.Fail(ConstantHelper.Message.HrUserNoEmployee);
            }
            return Results<BotEmployeeShiftModel>.Success(await Identity.BotEmployeeService.ShiftsAsync(_unitOfWork, _clock, employee.Id, days), ConstantHelper.Message.GetAllSuccessfully);
        }

        /// <summary>
        /// BOT-1.5c (DECISIONS p24 – 🔁 Đổi ca trên bot): với 1 ca CÒN MỞ của chính tài khoản, trả ca còn mở của đồng nghiệp cùng đơn vị (± 7 ngày quanh ca đó, từ hôm nay)
        /// để đổi chéo, và danh sách đồng nghiệp để nhường ca. Chỉ tên + ca. Quy tắc đổi (đã chấm công, trùng giờ, đơn đang chờ…) vẫn kiểm ở ShiftSwapRequest/Create.
        /// </summary>
        public async Task<IResult<BotSwapCandidatesModel>> GetSwapCandidates(Guid assignmentId)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var employee = await HrRules.ResolveEmployeeAsync(db, _currentActor.RequireActor().UserId);
                if (employee == null)
                {
                    return Result<BotSwapCandidatesModel>.Fail(ConstantHelper.Message.HrUserNoEmployee);
                }
                var mine = await db.ShiftAssignment.AsNoTracking().FirstOrDefaultAsync(a => a.Id == assignmentId && !a.IsDeleted && a.EmployeeId == employee.Id);
                if (mine == null || !ShiftAssignmentStatus.Open.Contains(mine.AssignmentStatus))
                {
                    return Result<BotSwapCandidatesModel>.Fail(ConstantHelper.Message.HrSwapAssignmentNotFound);
                }
                var shiftNames = await db.Shift.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name ?? s.Code);
                var colleagues = employee.OrganizationUnitId is not { } unit ? []
                    : (await db.Employee.AsNoTracking().Where(e => !e.IsDeleted && e.Id != employee.Id && e.OrganizationUnitId == unit)
                        .Select(e => new { e.Id, e.FullName, e.Code, e.Status }).ToListAsync())
                        .Where(e => EmployeeStatus.IsWorking(e.Status)).OrderBy(e => e.FullName).ToList();
                var names = colleagues.ToDictionary(e => e.Id, e => e.FullName ?? e.Code);
                var ids = names.Keys.ToList();
                DateTime today = _clock.Today, from = mine.WorkDate.Date.AddDays(-7), to = mine.WorkDate.Date.AddDays(8);
                if (from < today)
                {
                    from = today;
                }
                var open = ShiftAssignmentStatus.Open.ToList();
                var shifts = ids.Count == 0 ? []
                    : await db.ShiftAssignment.AsNoTracking()
                        .Where(a => !a.IsDeleted && ids.Contains(a.EmployeeId) && a.WorkDate >= from && a.WorkDate < to && open.Contains(a.AssignmentStatus!))
                        .OrderBy(a => a.WorkDate).ThenBy(a => a.PlannedStart).Take(60).ToListAsync();
                return Result<BotSwapCandidatesModel>.Success(new BotSwapCandidatesModel
                {
                    Current = new BotEmployeeShiftModel
                    {
                        AssignmentId = mine.Id, WorkDate = mine.WorkDate, ShiftName = shiftNames.GetValueOrDefault(mine.ShiftId),
                        PlannedStart = mine.PlannedStart, PlannedEnd = mine.PlannedEnd, AssignmentStatus = mine.AssignmentStatus,
                    },
                    ListShift = [.. shifts.Where(a => !(a.ShiftId == mine.ShiftId && a.WorkDate.Date == mine.WorkDate.Date)).Take(30).Select(a => new BotSwapCandidateModel
                    {
                        EmployeeId = a.EmployeeId, EmployeeName = names.GetValueOrDefault(a.EmployeeId), AssignmentId = a.Id, WorkDate = a.WorkDate,
                        ShiftName = shiftNames.GetValueOrDefault(a.ShiftId), PlannedStart = a.PlannedStart, PlannedEnd = a.PlannedEnd,
                    })],
                    ListColleague = [.. colleagues.Take(30).Select(e => new BotSwapCandidateModel { EmployeeId = e.Id, EmployeeName = e.FullName ?? e.Code })],
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetSwapCandidates: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>BOT-1.7: SHIFT_CONFIRMED – mỗi nhân viên 1 tin liệt kê các ca vừa xác nhận (chống trùng theo nhân viên + danh sách ca). Trả số tin đã xếp.</summary>
        private async Task<int> NotifyConfirmedAsync(List<ShiftAssignment> confirmed)
        {
            if (confirmed.Count == 0)
            {
                return 0;
            }
            int queued = 0;
            try
            {
                var shiftIds = confirmed.Select(a => a.ShiftId).Distinct().ToList();
                var names = await _unitOfWork.DbContext.Shift.AsNoTracking().Where(x => shiftIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name ?? x.Code);
                foreach (var g in confirmed.GroupBy(a => a.EmployeeId))
                {
                    var rows = g.OrderBy(a => a.WorkDate).ThenBy(a => a.PlannedStart).ToList();
                    string ids = string.Join(",", rows.Select(a => a.Id.ToString("N")).OrderBy(x => x));
                    string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ids)))[..12];
                    queued += await AppDelicare.Services.Implement.Messaging.BusinessNotifier.SafeEnqueueAsync(_notifications, new NotificationRequest
                    {
                        Template = AppDelicare.Services.Implement.Messaging.NotificationCatalog.ShiftConfirmed,
                        EmployeeIds = [g.Key],
                        Data = new
                        {
                            count = rows.Count,
                            lines = rows.Select(a => $"{a.WorkDate:dd/MM} {names.GetValueOrDefault(a.ShiftId)} {a.PlannedStart:HH:mm}–{a.PlannedEnd:HH:mm}").ToList(),
                        },
                        IdempotencyKey = $"SHIFT_CONFIRMED:{g.Key:N}:{hash}",
                        CorrelationId = $"SHIFT:{g.Key:N}",
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[ShiftAssignmentService] NotifyConfirmed");
            }
            return queued;
        }
    }
}
