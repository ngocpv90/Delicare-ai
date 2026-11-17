// Sinh bởi tools/AppDelicare.CodeGen (T05), nay là file của dev (BE2.6: vào / ra ca GPS – QR – máy – chấm hộ, bảng công) – CodeGen không ghi đè.
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
    public class AttendanceEventService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper, IReferenceGuard referenceGuard
        , IClock clock, ICurrentActor currentActor, IPermissionService permissionService, IOptions<BusinessRulesOptions> rules) : IAttendanceEventService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IReferenceGuard _referenceGuard = referenceGuard;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly IPermissionService _permissionService = permissionService;
        private readonly HrRulesOptions _hr = rules.Value.Hr;

        public async Task<IResults<AttendanceEventViewModel>> GetAll(AttendanceEventSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@EmployeeId", request.EmployeeId);
                dynamicParameters.Add("@ShiftAssignmentId", request.ShiftAssignmentId);
                dynamicParameters.Add("@EventType", request.EventType);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<AttendanceEventViewModel>("uspAttendanceEvent_GetAll", dynamicParameters)).ToList();
                int i = 0;
                list.ForEach(x =>
                {
                    i++;
                    x.STT = (request.Page - 1) * request.PageSize + i;
                });
                return new Results<AttendanceEventViewModel>
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

        public IResults<AttendanceEvent> GetAllData()
        {
            try
            {
                var list = _unitOfWork.AttendanceEventRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<AttendanceEvent>
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

        public async Task<IResult<bool>> Save(AttendanceEventRequest request, string typeSave)
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
                    case ConstantHelper.TypeSave.Delete:
                        result = "Sổ chỉ ghi thêm: không sửa / xóa. Dùng bút toán đảo (CLAUDE.md 3.3).";
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

        /// <summary>BE2.6: chấm công chỉ qua <c>AttendanceEvent/CheckIn</c> / <c>CheckOut</c> (kiểm ca, bán kính GPS, hình thức, chống trùng) – CRUD Insert không ghi.</summary>
        private Task<string> Insert(AttendanceEventRequest request, string typeSave) => Task.FromResult(ConstantHelper.Message.HrAttendanceCrudBlocked);

        public async Task<IResult<AttendanceEvent>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.AttendanceEventRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<AttendanceEvent>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<AttendanceEvent>
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

        // ============================================================ BE2.6 – chấm công (spec 11.2, Catalog 205) + bảng công (Catalog 206)
        public Task<IResult<AttendanceResultModel>> CheckIn(AttendanceCheckRequest request) => CheckAsync(request, true);

        public Task<IResult<AttendanceResultModel>> CheckOut(AttendanceCheckRequest request) => CheckAsync(request, false);

        private static int Minutes(TimeSpan span) => span <= TimeSpan.Zero ? 0 : (int)Math.Floor(span.TotalMinutes);

        /// <summary>
        /// Spec 11.2 check-in / check-out – 1 transaction, ghi thêm 1 <c>AttendanceEvent</c> (sổ chỉ ghi thêm):
        /// <list type="number">
        /// <item>Nhân viên: trống = nhân viên của tài khoản (GAP-HR-02); nhân viên khác cần quyền Thêm trên <c>#/hr-attendance</c> + ghi rõ hình thức (T21 AC "chấm hộ phải ghi rõ SourceChannel"),
        /// ghi "Ghi bởi &lt;tài khoản&gt;" vào Note. Tự chấm chỉ GPS / QR; chấm hộ (PROXY) bắt buộc lý do.</item>
        /// <item>Chống trùng: cùng <c>ExternalEventId</c> + nhân viên + loại → trả sự kiện đã ghi, không ghi thêm.</item>
        /// <item>Ca: vào ca = ca Kế hoạch / Xác nhận của hôm nay hoặc hôm qua (ca đêm) có giờ hiện tại trong [bắt đầu − <c>CheckInEarlyMinutes</c>, kết thúc + <c>CheckInLateMinutes</c>],
        /// gần giờ bắt đầu nhất; ra ca = ca Đã vào ca gần nhất. Không có → 400.</item>
        /// <item>Vị trí: <c>DistanceMeters</c> = khoảng cách tới trạm bếp (<c>KitchenWorkstation</c> Hoạt động có tọa độ) gần nhất – hoặc trạm theo <c>WorkstationCode</c>;
        /// GPS: <c>IsValid</c> = khoảng cách ≤ <c>RadiusMeters</c> (trống → <c>Hr:DefaultRadiusMeters</c>), ngoài bán kính vẫn ghi với IsValid = false + cảnh báo
        /// (<c>Hr:RejectOutsideRadius</c> = true → chặn). QR (mã trạm bắt buộc) / máy / chấm hộ: IsValid = true.</item>
        /// <item>Đi muộn / về sớm so với giờ kế hoạch của ca (ân hạn <c>LateGraceMinutes</c> / <c>EarlyLeaveGraceMinutes</c>) ghi vào Note. Ca → Đã vào ca / Hoàn thành.</item>
        /// </list>
        /// Tọa độ chỉ lưu ở AttendanceEvent (không ghi log, AuditChange che giá trị, không trả lại trong kết quả) – dữ liệu cá nhân.
        /// </summary>
        private async Task<IResult<AttendanceResultModel>> CheckAsync(AttendanceCheckRequest request, bool checkIn)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                Guid actor = _currentActor.RequireActor().UserId;
                DateTime now = _clock.Now, today = now.Date;
                string eventType = checkIn ? AttendanceEventType.CheckIn : AttendanceEventType.CheckOut;
                var self = await HrRules.ResolveEmployeeAsync(db, actor);
                bool forOther = request.EmployeeId is { } wanted && wanted != Guid.Empty && wanted != self?.Id;
                Employee? employee;
                if (forOther)
                {
                    var perms = await _permissionService.GetAsync(actor);
                    if (!_permissionService.IsAllowed(perms, HrScreen.Attendance, PermissionAction.Add))
                    {
                        return Fail(string.Format(ConstantHelper.Message.HrNoRightOther, "chấm công", "Chấm công theo ca"));
                    }
                    employee = await db.Employee.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.EmployeeId && !e.IsDeleted);
                }
                else
                {
                    if (self == null)
                    {
                        return Fail(ConstantHelper.Message.HrUserNoEmployee);
                    }
                    employee = self;
                }
                if (employee == null || !EmployeeStatus.IsWorking(employee.Status))
                {
                    return Fail(ConstantHelper.Message.HrEmployeeNotFound);
                }
                string? channel = AttendanceSource.Normalize(request.SourceChannel);
                if (!string.IsNullOrWhiteSpace(request.SourceChannel) && channel == null)
                {
                    return Fail(ConstantHelper.Message.HrSourceInvalid);
                }
                if (forOther && channel == null)
                {
                    return Fail(ConstantHelper.Message.HrSourceRequiredForOther);
                }
                channel ??= AttendanceSource.Gps;
                if (!forOther && channel is AttendanceSource.Machine or AttendanceSource.Proxy)
                {
                    return Fail(ConstantHelper.Message.HrSourceSelfInvalid);
                }
                if (channel == AttendanceSource.Proxy && string.IsNullOrWhiteSpace(request.Note))
                {
                    return Fail(ConstantHelper.Message.HrProxyNoteRequired);
                }
                string? externalId = string.IsNullOrWhiteSpace(request.ExternalEventId) ? null : request.ExternalEventId.Trim();
                if (externalId != null)
                {
                    var dup = await db.AttendanceEvent.AsNoTracking()
                        .FirstOrDefaultAsync(e => !e.IsDeleted && e.ExternalEventId == externalId && e.EmployeeId == employee.Id && e.EventType == eventType);
                    if (dup != null)
                    {
                        _unitOfWork.RollbackTransaction();
                        var dupModel = await ResultModelAsync(dup, employee, null, null, 0, 0);
                        dupModel.IsDuplicate = true;
                        return Result<AttendanceResultModel>.Success(dupModel, ConstantHelper.Message.HrDuplicateEvent);
                    }
                }

                // Ca của nhân viên (hôm qua – ca đêm, hôm nay).
                DateTime yesterday = today.AddDays(-1);
                var candidates = _unitOfWork.ShiftAssignmentRepository.GetAll()
                    .Where(a => !a.IsDeleted && a.EmployeeId == employee.Id && a.WorkDate >= yesterday && a.WorkDate <= today).ToList();
                var shiftIds = candidates.Select(c => c.ShiftId).Distinct().ToList();
                var shifts = await db.Shift.AsNoTracking().Where(s => shiftIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id);
                // Ca phân trước BE2.6 có thể thiếu giờ kế hoạch → tính theo giờ ca (không ghi lại vào dòng).
                var window = candidates.ToDictionary(c => c.Id, c => c.PlannedStart != null && c.PlannedEnd != null
                    ? (Start: c.PlannedStart.Value, End: c.PlannedEnd.Value)
                    : shifts.GetValueOrDefault(c.ShiftId) is { } s ? HrRules.PlannedWindow(c.WorkDate, s) : (Start: c.WorkDate.Date, End: c.WorkDate.Date.AddDays(1)));
                ShiftAssignment? assignment;
                if (checkIn)
                {
                    assignment = candidates
                        .Where(a => ShiftAssignmentStatus.Open.Contains(a.AssignmentStatus)
                            && now >= window[a.Id].Start.AddMinutes(-_hr.CheckInEarlyMinutes) && now <= window[a.Id].End.AddMinutes(_hr.CheckInLateMinutes))
                        .OrderBy(a => Math.Abs((now - window[a.Id].Start).TotalMinutes)).FirstOrDefault();
                    if (assignment == null)
                    {
                        if (candidates.Where(a => a.AssignmentStatus == ShiftAssignmentStatus.CheckedIn).OrderByDescending(a => a.PlannedStart).FirstOrDefault() is { } open)
                        {
                            return Fail(string.Format(ConstantHelper.Message.HrAlreadyCheckedIn, employee.FullName, shifts.GetValueOrDefault(open.ShiftId)?.Code, HrRules.Day(open.WorkDate)));
                        }
                        var next = candidates.Where(a => ShiftAssignmentStatus.Open.Contains(a.AssignmentStatus) && window[a.Id].Start > now).OrderBy(a => window[a.Id].Start).FirstOrDefault();
                        return Fail(string.Format(ConstantHelper.Message.HrNoShiftToCheckIn, employee.FullName)
                            + (next != null ? string.Format(ConstantHelper.Message.HrNextShiftHint, shifts.GetValueOrDefault(next.ShiftId)?.Code,
                                window[next.Id].Start.AddMinutes(-_hr.CheckInEarlyMinutes).ToString("HH:mm dd/MM", CultureInfo.InvariantCulture)) : string.Empty));
                    }
                }
                else
                {
                    assignment = candidates.Where(a => a.AssignmentStatus == ShiftAssignmentStatus.CheckedIn).OrderByDescending(a => window[a.Id].Start).FirstOrDefault();
                    if (assignment == null)
                    {
                        return Fail(string.Format(ConstantHelper.Message.HrNoShiftToCheckOut, employee.FullName));
                    }
                }
                var (plannedStart, plannedEnd) = window[assignment.Id];

                // Vị trí / hình thức.
                var warnings = new List<string>();
                var stations = await db.KitchenWorkstation.AsNoTracking().Where(w => !w.IsDeleted && w.Status == "Active").ToListAsync();
                KitchenWorkstation? station = null;
                if (!string.IsNullOrWhiteSpace(request.WorkstationCode))
                {
                    string code = request.WorkstationCode.Trim();
                    station = stations.FirstOrDefault(w => string.Equals(w.Code, code, StringComparison.OrdinalIgnoreCase));
                    if (station == null)
                    {
                        return Fail(string.Format(ConstantHelper.Message.HrWorkstationNotFound, code));
                    }
                }
                decimal? lat = request.Latitude is { } la ? Math.Round(la, 7) : null;
                decimal? lon = request.Longitude is { } lo ? Math.Round(lo, 7) : null;
                decimal? distance = null, radius = null;
                if (lat != null && lon != null)
                {
                    var withGps = stations.Where(w => w.Latitude != null && w.Longitude != null).ToList();
                    var pick = station != null && station.Latitude != null && station.Longitude != null
                        ? station
                        : withGps.OrderBy(w => HrRules.DistanceMeters(lat.Value, lon.Value, w.Latitude!.Value, w.Longitude!.Value)).FirstOrDefault();
                    if (pick != null)
                    {
                        station = pick;
                        distance = HrRules.DistanceMeters(lat.Value, lon.Value, pick.Latitude!.Value, pick.Longitude!.Value);
                        radius = pick.RadiusMeters is > 0 ? pick.RadiusMeters : _hr.DefaultRadiusMeters;
                    }
                }
                bool? valid;
                switch (channel)
                {
                    case AttendanceSource.Gps:
                        if (lat == null || lon == null)
                        {
                            return Fail(ConstantHelper.Message.HrGpsRequired);
                        }
                        if (distance == null)
                        {
                            valid = false;
                            warnings.Add(ConstantHelper.Message.HrNoWorkstationGps);
                        }
                        else
                        {
                            valid = distance <= radius;
                            if (valid == false)
                            {
                                string outside = string.Format(ConstantHelper.Message.HrOutsideRadius, station!.Code, distance.Value.ToString("0", CultureInfo.InvariantCulture), radius!.Value.ToString("0", CultureInfo.InvariantCulture));
                                if (_hr.RejectOutsideRadius)
                                {
                                    return Fail(outside);
                                }
                                warnings.Add(outside);
                            }
                        }
                        break;
                    case AttendanceSource.Qr:
                        if (string.IsNullOrWhiteSpace(request.WorkstationCode))
                        {
                            return Fail(ConstantHelper.Message.HrQrWorkstationRequired);
                        }
                        valid = true;
                        break;
                    default:
                        valid = true;
                        break;
                }

                // Đi muộn / về sớm.
                int late = 0, early = 0;
                if (checkIn)
                {
                    late = Minutes(now - plannedStart);
                    late = late > _hr.LateGraceMinutes ? late : 0;
                }
                else
                {
                    early = Minutes(plannedEnd - now);
                    early = early > _hr.EarlyLeaveGraceMinutes ? early : 0;
                }
                var notes = new List<string>();
                if (station != null)
                {
                    notes.Add("Trạm " + station.Code);
                }
                if (late > 0)
                {
                    notes.Add($"Đi muộn {late} phút");
                }
                if (early > 0)
                {
                    notes.Add($"Về sớm {early} phút");
                }
                if (valid == false)
                {
                    notes.Add("Ngoài bán kính / chưa xác minh vị trí");
                }
                if (forOther)
                {
                    string? userName = await db.User.AsNoTracking().Where(u => u.Id == actor).Select(u => u.UserName).FirstOrDefaultAsync();
                    notes.Add($"Ghi bởi {userName} ({AttendanceSource.Label(channel)})");
                }
                if (!string.IsNullOrWhiteSpace(request.Note))
                {
                    notes.Add(request.Note.Trim());
                }
                string note = string.Join("; ", notes);
                var ev = new AttendanceEvent
                {
                    Id = Guid.NewGuid(), EmployeeId = employee.Id, ShiftAssignmentId = assignment.Id, EventType = eventType, EventAt = now,
                    Latitude = lat, Longitude = lon, DistanceMeters = distance, IsValid = valid, SourceChannel = channel, ExternalEventId = externalId,
                    Note = note.Length <= 1000 ? note : note[..1000],
                };
                _unitOfWork.AttendanceEventRepository.Add(ev);
                assignment.AssignmentStatus = checkIn ? ShiftAssignmentStatus.CheckedIn : ShiftAssignmentStatus.Completed;
                _unitOfWork.ShiftAssignmentRepository.Update(assignment);
                await _unitOfWork.AttendanceEventRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                var model = await ResultModelAsync(ev, employee, station, radius, late, early);
                model.ListWarning.AddRange(warnings);
                string shiftCode = model.Assignment?.ShiftCode ?? string.Empty;
                return Result<AttendanceResultModel>.Success(model, string.Format(checkIn ? ConstantHelper.Message.HrCheckedIn : ConstantHelper.Message.HrCheckedOut, shiftCode, employee.FullName)
                    + (warnings.Count > 0 ? " – " + string.Join("; ", warnings) : string.Empty));
            }
            catch (Exception ex)
            {
                // Không ghi request (có tọa độ) vào log.
                Log.Error(string.Concat("[", GetType().Name, "]", checkIn ? " CheckIn: " : " CheckOut: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<AttendanceResultModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<AttendanceResultModel>.Fail(message);
            }
        }

        private async Task<AttendanceResultModel> ResultModelAsync(AttendanceEvent ev, Employee employee, KitchenWorkstation? station, decimal? radius, int late, int early)
        {
            var db = _unitOfWork.DbContext;
            HrAssignmentModel? assignment = null;
            if (ev.ShiftAssignmentId is { } aid && await db.ShiftAssignment.AsNoTracking().FirstOrDefaultAsync(a => a.Id == aid) is { } row)
            {
                assignment = (await HrRules.ModelsAsync(db, [row])).FirstOrDefault();
            }
            return new AttendanceResultModel
            {
                EventId = ev.Id, EmployeeId = employee.Id, EmployeeName = employee.FullName, EventType = ev.EventType, EventTypeName = AttendanceEventType.Label(ev.EventType),
                EventAt = ev.EventAt, SourceChannel = ev.SourceChannel, SourceChannelName = AttendanceSource.Label(ev.SourceChannel), IsValid = ev.IsValid,
                DistanceMeters = ev.DistanceMeters, RadiusMeters = radius, WorkstationCode = station?.Code, WorkstationName = station?.Name,
                Assignment = assignment, LateMinutes = late, EarlyLeaveMinutes = early, Note = ev.Note,
            };
        }

        /// <summary>Bảng công chi tiết cho các ca: giờ vào / ra (lần vào đầu tiên, lần ra cuối cùng), đi muộn / về sớm, giờ công, vắng, thiếu ra ca.</summary>
        private async Task<List<TimesheetDetailModel>> DetailsAsync(IReadOnlyCollection<ShiftAssignment> rows)
        {
            var db = _unitOfWork.DbContext;
            var ids = rows.Select(r => r.Id).ToList();
            var events = await db.AttendanceEvent.AsNoTracking().Where(e => !e.IsDeleted && e.ShiftAssignmentId != null && ids.Contains(e.ShiftAssignmentId.Value)).ToListAsync();
            var models = (await HrRules.ModelsAsync(db, rows)).ToDictionary(m => m.Id);
            DateTime now = _clock.Now;
            var list = new List<TimesheetDetailModel>();
            foreach (var r in rows)
            {
                var m = models[r.Id];
                var inEv = events.Where(e => e.ShiftAssignmentId == r.Id && e.EventType == AttendanceEventType.CheckIn).OrderBy(e => e.EventAt).FirstOrDefault();
                var outEv = events.Where(e => e.ShiftAssignmentId == r.Id && e.EventType == AttendanceEventType.CheckOut).OrderByDescending(e => e.EventAt).FirstOrDefault();
                int late = inEv != null && r.PlannedStart is { } ps ? Minutes(inEv.EventAt - ps) : 0;
                int early = outEv != null && r.PlannedEnd is { } pe ? Minutes(pe - outEv.EventAt) : 0;
                list.Add(new TimesheetDetailModel
                {
                    AssignmentId = r.Id, EmployeeId = r.EmployeeId, EmployeeCode = m.EmployeeCode, EmployeeName = m.EmployeeName, WorkDate = r.WorkDate,
                    ShiftId = r.ShiftId, ShiftCode = m.ShiftCode, ShiftName = m.ShiftName, PlannedStart = r.PlannedStart, PlannedEnd = r.PlannedEnd,
                    Status = r.AssignmentStatus, StatusName = ShiftAssignmentStatus.Label(r.AssignmentStatus),
                    CheckInAt = inEv?.EventAt, CheckOutAt = outEv?.EventAt, CheckInSource = inEv?.SourceChannel, CheckOutSource = outEv?.SourceChannel,
                    CheckInValid = inEv?.IsValid, CheckOutValid = outEv?.IsValid, CheckInDistanceMeters = inEv?.DistanceMeters, CheckOutDistanceMeters = outEv?.DistanceMeters,
                    LateMinutes = late > _hr.LateGraceMinutes ? late : 0,
                    EarlyLeaveMinutes = early > _hr.EarlyLeaveGraceMinutes ? early : 0,
                    WorkedMinutes = inEv != null && outEv != null ? Minutes(outEv.EventAt - inEv.EventAt) : 0,
                    IsAbsent = r.AssignmentStatus == ShiftAssignmentStatus.Absent
                        || (inEv == null && ShiftAssignmentStatus.Open.Contains(r.AssignmentStatus) && r.PlannedEnd != null && r.PlannedEnd < now),
                    MissingCheckOut = inEv != null && outEv == null && r.PlannedEnd != null && r.PlannedEnd < now,
                });
            }
            return [.. list.OrderBy(x => x.WorkDate).ThenBy(x => x.PlannedStart).ThenBy(x => x.EmployeeName)];
        }

        /// <summary>Nhân viên trong phạm vi (đơn vị + con, 1 nhân viên, tìm theo mã / tên) – gồm cả nhân viên đã nghỉ việc (bảng công quá khứ).</summary>
        private async Task<List<Employee>> TimesheetEmployeesAsync(TimesheetRequest request)
        {
            var db = _unitOfWork.DbContext;
            var scope = await HrRules.OrganizationScopeAsync(db, request.OrganizationUnitId);
            var query = db.Employee.AsNoTracking().Where(e => !e.IsDeleted);
            if (request.EmployeeId is { } eid && eid != Guid.Empty)
            {
                query = query.Where(e => e.Id == eid);
            }
            if (scope != null)
            {
                query = query.Where(e => e.OrganizationUnitId != null && scope.Contains(e.OrganizationUnitId.Value));
            }
            if (!string.IsNullOrWhiteSpace(request.SearchText))
            {
                string s = request.SearchText.Trim();
                query = query.Where(e => (e.FullName != null && e.FullName.Contains(s)) || (e.Code != null && e.Code.Contains(s)) || (e.EmployeeCode != null && e.EmployeeCode.Contains(s)));
            }
            return [.. (await query.ToListAsync()).OrderBy(e => e.FullName)];
        }

        private string? RangeError(TimesheetRequest request)
            => request.FromDate == default || request.ToDate == default || request.ToDate.Date < request.FromDate.Date
                || (request.ToDate.Date - request.FromDate.Date).TotalDays + 1 > _hr.TimesheetMaxDays
                ? string.Format(ConstantHelper.Message.HrDateRangeInvalid, _hr.TimesheetMaxDays) : null;

        /// <summary>
        /// Spec 11.2 GET attendance/report (Catalog 206 – dạng danh sách, không phải báo cáo): mỗi nhân viên 1 dòng – số ca được phân, có mặt, đi muộn (+ phút), về sớm (+ phút),
        /// vắng (trạng thái Vắng hoặc ca hết giờ chưa vào ca – không cần job, D-45), thiếu ra ca, lần chấm ngoài bán kính, ngày nghỉ (đơn Đã duyệt), giờ công. Phân trang.
        /// </summary>
        public async Task<IResults<TimesheetRowModel>> GetTimesheet(TimesheetRequest request)
        {
            try
            {
                if (RangeError(request) is { } error)
                {
                    return new Results<TimesheetRowModel> { ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest), ResponseMessage = error, ListData = [], TotalRecords = 0 };
                }
                var db = _unitOfWork.DbContext;
                DateTime from = request.FromDate.Date, to = request.ToDate.Date;
                var employees = await TimesheetEmployeesAsync(request);
                int page = Math.Max(request.Page, 1), size = Math.Clamp(request.PageSize, 1, 1000);
                var pageEmployees = employees.Skip((page - 1) * size).Take(size).ToList();
                var empIds = pageEmployees.Select(e => e.Id).ToList();
                var rows = await db.ShiftAssignment.AsNoTracking().Where(a => !a.IsDeleted && empIds.Contains(a.EmployeeId) && a.WorkDate >= from && a.WorkDate <= to).ToListAsync();
                var details = await DetailsAsync(rows);
                var ids = rows.Select(r => r.Id).ToList();
                var invalid = await db.AttendanceEvent.AsNoTracking()
                    .Where(e => !e.IsDeleted && e.IsValid == false && e.ShiftAssignmentId != null && ids.Contains(e.ShiftAssignmentId.Value))
                    .GroupBy(e => e.EmployeeId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
                var leaves = await HrRules.LeavesAsync(db, empIds, from, to, true);
                var positionIds = pageEmployees.Where(e => e.PositionId != null).Select(e => e.PositionId!.Value).Distinct().ToList();
                var ouIds = pageEmployees.Where(e => e.OrganizationUnitId != null).Select(e => e.OrganizationUnitId!.Value).Distinct().ToList();
                var positions = await db.Position.AsNoTracking().Where(p => positionIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name);
                var ous = await db.OrganizationUnit.AsNoTracking().Where(o => ouIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Name);
                int stt = (page - 1) * size;
                var list = pageEmployees.Select(e =>
                {
                    var mine = details.Where(d => d.EmployeeId == e.Id).ToList();
                    int leaveDays = leaves.Where(l => l.EmployeeId == e.Id).Sum(l =>
                    {
                        DateTime f = l.FromDate.Date > from ? l.FromDate.Date : from, t = l.ToDate.Date < to ? l.ToDate.Date : to;
                        return t >= f ? (int)(t - f).TotalDays + 1 : 0;
                    });
                    int worked = mine.Sum(d => d.WorkedMinutes);
                    return new TimesheetRowModel
                    {
                        STT = ++stt, EmployeeId = e.Id, EmployeeCode = e.Code ?? e.EmployeeCode, FullName = e.FullName,
                        OrganizationUnitName = e.OrganizationUnitId is { } o ? ous.GetValueOrDefault(o) : null,
                        PositionName = e.PositionId is { } p ? positions.GetValueOrDefault(p) : null,
                        AssignedShifts = mine.Count,
                        Present = mine.Count(d => d.CheckInAt != null),
                        Late = mine.Count(d => d.LateMinutes > 0), LateMinutes = mine.Sum(d => d.LateMinutes),
                        EarlyLeave = mine.Count(d => d.EarlyLeaveMinutes > 0), EarlyLeaveMinutes = mine.Sum(d => d.EarlyLeaveMinutes),
                        Absent = mine.Count(d => d.IsAbsent), MissingCheckOut = mine.Count(d => d.MissingCheckOut),
                        InvalidEvents = invalid.GetValueOrDefault(e.Id), LeaveDays = leaveDays,
                        WorkedMinutes = worked, WorkedHours = Math.Round(worked / 60m, 2),
                    };
                }).ToList();
                return new Results<TimesheetRowModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = employees.Count,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetTimesheet: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Catalog 206 (chi tiết): mỗi ca 1 dòng – giờ vào / ra, hình thức, hợp lệ, khoảng cách, đi muộn / về sớm, giờ công, vắng.</summary>
        public async Task<IResults<TimesheetDetailModel>> GetTimesheetDetail(TimesheetRequest request)
        {
            try
            {
                if (RangeError(request) is { } error)
                {
                    return new Results<TimesheetDetailModel> { ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest), ResponseMessage = error, ListData = [], TotalRecords = 0 };
                }
                var db = _unitOfWork.DbContext;
                DateTime from = request.FromDate.Date, to = request.ToDate.Date;
                var empIds = (await TimesheetEmployeesAsync(request)).Select(e => e.Id).ToList();
                var rows = await db.ShiftAssignment.AsNoTracking().Where(a => !a.IsDeleted && empIds.Contains(a.EmployeeId) && a.WorkDate >= from && a.WorkDate <= to).ToListAsync();
                var details = await DetailsAsync(rows);
                int page = Math.Max(request.Page, 1), size = Math.Clamp(request.PageSize, 1, 1000);
                var list = details.Skip((page - 1) * size).Take(size).ToList();
                return new Results<TimesheetDetailModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = details.Count,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetTimesheetDetail: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>BE2.6: ca hôm nay (+ ca đêm hôm qua) của nhân viên đang đăng nhập và hành động làm được (vào / ra ca) – màn chấm công tự phục vụ.</summary>
        public async Task<IResult<MyAttendanceModel>> GetMine()
        {
            try
            {
                var db = _unitOfWork.DbContext;
                Guid actor = _currentActor.RequireActor().UserId;
                var employee = await HrRules.ResolveEmployeeAsync(db, actor);
                if (employee == null)
                {
                    return Result<MyAttendanceModel>.Fail(ConstantHelper.Message.HrUserNoEmployee);
                }
                DateTime now = _clock.Now, today = now.Date, yesterday = today.AddDays(-1);
                var rows = await db.ShiftAssignment.AsNoTracking().Where(a => !a.IsDeleted && a.EmployeeId == employee.Id && a.WorkDate >= yesterday && a.WorkDate <= today).ToListAsync();
                rows = [.. rows.Where(r => r.WorkDate.Date == today || r.AssignmentStatus == ShiftAssignmentStatus.CheckedIn || (r.PlannedEnd != null && r.PlannedEnd > now))];
                var model = new MyAttendanceModel
                {
                    EmployeeId = employee.Id, EmployeeCode = employee.Code ?? employee.EmployeeCode, EmployeeName = employee.FullName,
                    ListAssignment = await DetailsAsync(rows),
                };
                if (rows.Any(r => r.AssignmentStatus == ShiftAssignmentStatus.CheckedIn))
                {
                    model.AllowedActions.Add("checkOut");
                }
                else if (rows.Any(r => ShiftAssignmentStatus.Open.Contains(r.AssignmentStatus) && r.PlannedStart != null && r.PlannedEnd != null
                    && now >= r.PlannedStart.Value.AddMinutes(-_hr.CheckInEarlyMinutes) && now <= r.PlannedEnd.Value.AddMinutes(_hr.CheckInLateMinutes)))
                {
                    model.AllowedActions.Add("checkIn");
                }
                return Result<MyAttendanceModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMine: ", ex.ToString()));
                throw;
            }
        }
    }
}
