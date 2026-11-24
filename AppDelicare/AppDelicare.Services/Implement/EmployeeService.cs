using AutoMapper;
using Dapper;
using Microsoft.EntityFrameworkCore;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class EmployeeService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IDocumentNumberService documentNumberService
        , AppDelicare.Data.Share.IClock clock, AppDelicare.Data.Share.ICurrentActor currentActor, IPermissionService permissionService
        , IAttendanceEventService attendanceEventService, IAccountLifecycleService accountLifecycle) : IEmployeeService
    {
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        /// <summary>MA-1.3 (D-63): mã vừa cấp khi thêm mới – báo kèm trong thông báo thành công.</summary>
        private string? _createdCode;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        /// <summary>
        /// D-68 e (anh chốt mở rộng 2026-11-19 cho cả API cũ): ngày sinh, giới tính, địa chỉ chỉ trả cho người có quyền Sửa adm-employee
        /// (NS, quản trị). Không có người dùng (job / hệ thống) → giữ nguyên.
        /// </summary>
        private async Task<bool> CanViewPersonalAsync()
        {
            if (currentActor.Actor is not { } actor)
            {
                return true;
            }
            return permissionService.IsAllowed(await permissionService.GetAsync(actor.UserId), "adm-employee", PermissionAction.Edit);
        }

        private static void MaskPersonal(Employee e)
        {
            e.DateOfBirth = null;
            e.Gender = null;
            e.Address = null;
        }

        public async Task<IResults<EmployeeViewModel>> GetAll(EmployeeSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@CompanyId", request.CompanyId);
                dynamicParameters.Add("@OrganizationUnitId", request.OrganizationUnitId);
                dynamicParameters.Add("@PositionId", request.PositionId);
                dynamicParameters.Add("@Code", request.Code);
                dynamicParameters.Add("@FullName", request.FullName);
                dynamicParameters.Add("@DateOfBirth", request.DateOfBirth);
                //dynamicParameters.Add("@Gender", request.Gender);
                dynamicParameters.Add("@Phone", request.Phone);
                dynamicParameters.Add("@Email", request.Email);
                //dynamicParameters.Add("@Address", request.Address);
                dynamicParameters.Add("@HireDate", request.HireDate);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<EmployeeViewModel>("uspEmployee_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0 && !await CanViewPersonalAsync())
                {
                    list.ForEach(x => { x.DateOfBirth = null; x.Gender = null; x.Address = null; });
                }
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<EmployeeViewModel>
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

        public IResults<Employee> GetAllData()
        {
            try
            {
                var list = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking(_unitOfWork.DbContext.Employee).Where(x => !x.IsDeleted).ToList();
                if (list.Count != 0 && !CanViewPersonalAsync().GetAwaiter().GetResult())
                {
                    list.ForEach(MaskPersonal);
                }
                return new Results<Employee>
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

        public async Task<IResult<bool>> Save(EmployeeRequest request, string typeSave)
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
                    ResponseMessage = typeSave == ConstantHelper.TypeSave.Delete ? ConstantHelper.Message.DeleteSuccess
                        : _createdCode != null ? string.Format(ConstantHelper.Message.SaveSuccessWithCode, "nhân viên", _createdCode)
                        : ConstantHelper.Message.SaveSuccess,
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

        private async Task<string> Insert(EmployeeRequest request, string typeSave)
        {
            var insert = _mapper.Map<Employee>(request);
            // MA-1.3 (D-63): mã danh mục máy chủ tự sinh (NV…), bỏ qua giá trị gửi lên; không đổi sau khi cấp.
            insert.Code = await _documentNumberService.NextCodeAsync(DocumentKind.Employee);
            insert.EmployeeCode = insert.Code; // cột cũ ghi song song (DEPRECATED_COLUMNS)
            _createdCode = insert.Code;
            _unitOfWork.EmployeeRepository.Add(insert);
            await _unitOfWork.EmployeeRepository.CommitAsync(typeSave);
            // HR-ACC-1.6 (D-82 f): ô "Cấp tài khoản web" → yêu cầu duyệt "Cấp tài khoản" cho FIN-IT (cùng transaction).
            return await accountLifecycle.AfterEmployeeSavedAsync(null, null, insert, request.RequestWebAccount, request.AccountGroups) ?? string.Empty;
        }

        private async Task<string> Update(EmployeeRequest request, string typeSave)
        {
            var exist = _unitOfWork.EmployeeRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // MA-1.3 (D-63): mã không đổi sau khi cấp – bỏ qua giá trị gửi lên.
            request.Code = exist.Code;
            string? oldStatus = exist.Status;
            Guid? oldPosition = exist.PositionId;
            var update = _mapper.Map(request, exist);
            _unitOfWork.EmployeeRepository.Update(update);
            await _unitOfWork.EmployeeRepository.CommitAsync(typeSave);
            // HR-ACC-1.5 / 1.6 / 1.7 (D-82 e–h): đồng bộ Person, nghỉ việc → khóa tài khoản ngay, quay lại → yêu cầu mở lại, đổi chức danh → yêu cầu đổi nhóm.
            return await accountLifecycle.AfterEmployeeSavedAsync(oldStatus ?? EmployeeStatus.Active, oldPosition, update, request.RequestWebAccount, request.AccountGroups) ?? string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(Employee), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.EmployeeRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.EmployeeRepository.Update(delete);
            await _unitOfWork.EmployeeRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<Employee>> GetById(Guid id)
        {
            try
            {
                var data = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                    Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking(_unitOfWork.DbContext.Employee), x => x.Id == id && !x.IsDeleted);
                if (data != null && !await CanViewPersonalAsync())
                {
                    MaskPersonal(data);
                }
                if (data == null)
                {
                    return new Result<Employee>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<Employee>
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
        /// <summary>
        /// UI-4 (D-68 g1): hồ sơ nhân viên. Tài khoản web: PersonLink.UserOfEmployeeAsync (HR-ACC-1.3, D-82 i).
        /// Chỉ số / ca / giờ công lấy từ bảng công (AttendanceEventService.GetTimesheetDetail – cùng quy tắc muộn / vắng). D-68 e: ngày sinh, giới tính, địa chỉ
        /// chỉ trả về khi người xem có quyền Sửa adm-employee.
        /// </summary>
        public async Task<IResult<AppDelicare.Model.Model.EmployeeOverviewModel>> GetOverview(Guid id)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var e = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                    Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking(db.Employee), x => x.Id == id && !x.IsDeleted);
                if (e == null)
                {
                    return Result<AppDelicare.Model.Model.EmployeeOverviewModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                bool personal = await CanViewPersonalAsync();
                if (!personal)
                {
                    MaskPersonal(e);
                }
                DateTime today = clock.Today, now = clock.Now;
                var m = new AppDelicare.Model.Model.EmployeeOverviewModel { Employee = e, CanViewPersonal = personal };
                m.OrganizationUnitName = e.OrganizationUnitId is { } ou ? db.OrganizationUnit.Where(o => o.Id == ou).Select(o => o.Name).FirstOrDefault() : null;
                m.PositionName = e.PositionId is { } po ? db.Position.Where(o => o.Id == po).Select(o => o.Name).FirstOrDefault() : null;
                if (e.HireDate is { } hd && hd.Date <= today)
                {
                    m.TenureMonths = (today.Year - hd.Year) * 12 + today.Month - hd.Month - (today.Day < hd.Day ? 1 : 0);
                }

                // Tài khoản web + bot.
                var identities = db.UserExternalIdentity.Where(x => !x.IsDeleted && x.EmployeeId == id).OrderByDescending(x => x.LinkedAt).ToList();
                var usable = identities.FirstOrDefault(x => x.IdentityStatus == null || x.IdentityStatus == "Active" || x.IdentityStatus == "PendingCode");
                // HR-ACC-1.3 (D-82 i): tài khoản = PersonLink (Employee.PersonId → User.PersonId; dự phòng đoán cũ có cảnh báo) – cùng cách với chấm công / bot.
                var user = await AppDelicare.Services.Implement.Identity.PersonLink.UserOfEmployeeAsync(db, e);
                if (user != null)
                {
                    m.UserId = user.Id;
                    m.UserName = user.UserName;
                    m.UserIsActive = user.IsActive;
                    m.ListUserGroupName = (from c in db.UserClaim
                                           join g in db.UserGroup on c.UserGroupId equals g.Id
                                           where c.UserId == user.Id && !c.IsDeleted && !g.IsDeleted
                                           orderby g.Name
                                           select g.Name!).ToList();
                }
                m.BotLinked = usable != null;
                m.BotStatus = (usable ?? identities.FirstOrDefault())?.IdentityStatus;
                m.BotUsername = usable?.ExternalUsername ?? usable?.DisplayName;
                m.BotLinkedAt = usable?.LinkedAt;

                // Bảng công: đầu tháng (hoặc 29 ngày trước) → 7 ngày tới.
                DateTime monthStart = new(today.Year, today.Month, 1), from30 = today.AddDays(-29);
                DateTime from = monthStart < from30 ? monthStart : from30, to = today.AddDays(7);
                var details = (await attendanceEventService.GetTimesheetDetail(new AppDelicare.Model.Request.Custom.TimesheetRequest { EmployeeId = id, FromDate = from, ToDate = to, Page = 1, PageSize = 1000 })).ListData?.ToList() ?? [];
                var month = details.Where(d => d.WorkDate >= monthStart && d.WorkDate <= today).ToList();
                var present = month.Where(d => d.CheckInAt != null).ToList();
                m.MonthShifts = month.Count;
                m.MonthWorkedHours = Math.Round(month.Sum(d => d.WorkedMinutes) / 60m, 1);
                m.MonthLate = month.Count(d => d.LateMinutes > 0);
                m.MonthAbsent = month.Count(d => d.IsAbsent);
                m.MonthOnTimePct = present.Count > 0 ? Math.Round(present.Count(d => d.LateMinutes == 0) * 100m / present.Count, 1) : null;
                m.ListUpcomingShift = [.. details.Where(d => d.WorkDate >= today && d.WorkDate <= to)];
                m.ListRecentAttendance = [.. details.Where(d => d.CheckInAt != null || d.CheckOutAt != null).OrderByDescending(d => d.WorkDate).ThenByDescending(d => d.PlannedStart).Take(10)];
                for (DateTime d = from30; d <= today; d = d.AddDays(1))
                {
                    m.ListDayHours.Add(new AppDelicare.Model.Model.EmployeeDayHoursModel { Date = d, Hours = Math.Round(details.Where(x => x.WorkDate.Date == d).Sum(x => x.WorkedMinutes) / 60m, 1), PlannedHours = Math.Round((decimal)details.Where(x => x.WorkDate.Date == d && x.PlannedStart != null && x.PlannedEnd != null && x.PlannedEnd > x.PlannedStart).Sum(x => (x.PlannedEnd!.Value - x.PlannedStart!.Value).TotalHours), 1) });
                }

                // Nghỉ phép năm nay (Đã duyệt) theo loại + ngày nghỉ 7 ngày tới; đơn chờ duyệt.
                DateTime yearStart = new(today.Year, 1, 1), yearEnd = new(today.Year, 12, 31);
                var leaves = await HrRules.LeavesAsync(db, [id], yearStart, to > yearEnd ? to : yearEnd, true);
                int Days(DateTime f, DateTime t, DateTime a, DateTime b) { DateTime x = f.Date > a ? f.Date : a, y = t.Date < b ? t.Date : b; return y >= x ? (int)(y - x).TotalDays + 1 : 0; }
                m.ListLeaveByType = [.. leaves.GroupBy(l => l.LeaveTypeCode).Select(g => new AppDelicare.Model.Model.EmployeeLeaveTypeModel { LeaveTypeCode = g.Key, Days = g.Sum(l => Days(l.FromDate, l.ToDate, yearStart, yearEnd)) }).Where(x => x.Days > 0).OrderByDescending(x => x.Days)];
                m.YearLeaveDays = m.ListLeaveByType.Sum(x => x.Days);
                for (DateTime d = today; d <= to; d = d.AddDays(1))
                {
                    if (leaves.Any(l => l.FromDate.Date <= d && l.ToDate.Date >= d))
                    {
                        m.ListLeaveDate.Add(d);
                    }
                }
                m.PendingRequestCount = db.LeaveRequest.Count(l => !l.IsDeleted && l.EmployeeId == id && l.RequestStatus == LeaveRequestStatus.Pending)
                    + db.ShiftSwapRequest.Count(s => !s.IsDeleted && (s.RequesterEmployeeId == id || s.TargetEmployeeId == id) && s.RequestStatus == ShiftSwapStatus.Pending);

                // UI-2R (D-69 g–h): ca thường làm, tạo / cập nhật bởi, đơn gần đây.
                var usual = details.Where(d => d.ShiftCode != null).GroupBy(d => d.ShiftCode).OrderByDescending(g => g.Count()).FirstOrDefault();
                if (usual != null)
                {
                    var sample = usual.OrderByDescending(d => d.WorkDate).First();
                    m.UsualShiftCode = usual.Key;
                    m.UsualShiftStart = sample.PlannedStart;
                    m.UsualShiftEnd = sample.PlannedEnd;
                }
                var auditNames = await InfoPanelRules.UserNamesAsync(db, [e.CreatedBy, e.ModifiedBy]);
                m.CreatedByName = auditNames.NameOf(e.CreatedBy);
                m.ModifiedByName = auditNames.NameOf(e.ModifiedBy);
                var leaveReqs = await db.LeaveRequest.AsNoTracking().Where(l => !l.IsDeleted && l.EmployeeId == id)
                    .OrderByDescending(l => l.SubmittedAt).Take(5).ToListAsync();
                var swaps = await db.ShiftSwapRequest.AsNoTracking().Where(s => !s.IsDeleted && (s.RequesterEmployeeId == id || s.TargetEmployeeId == id))
                    .OrderByDescending(s => s.SubmittedAt).Take(5).ToListAsync();
                var otherIds = swaps.Select(s => s.RequesterEmployeeId == id ? s.TargetEmployeeId : s.RequesterEmployeeId).Distinct().ToList();
                var others = await db.Employee.AsNoTracking().Where(x => otherIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName);
                m.ListRecentRequest = [.. leaveReqs.Select(l => new AppDelicare.Model.Model.EmployeeRequestBriefModel
                    {
                        Kind = "LEAVE", Id = l.Id, RequestNo = l.RequestNo, LeaveTypeCode = l.LeaveTypeCode, FromDate = l.FromDate, ToDate = l.ToDate,
                        Days = (int)(l.ToDate.Date - l.FromDate.Date).TotalDays + 1, RequestStatus = l.RequestStatus, SubmittedAt = l.SubmittedAt,
                    })
                    .Concat(swaps.Select(s => new AppDelicare.Model.Model.EmployeeRequestBriefModel
                    {
                        Kind = "SWAP", Id = s.Id, RequestNo = s.RequestNo, RequestStatus = s.RequestStatus, SubmittedAt = s.SubmittedAt,
                        OtherEmployeeName = others.TryGetValue(s.RequesterEmployeeId == id ? s.TargetEmployeeId : s.RequesterEmployeeId, out var on) ? on : null,
                    }))
                    .OrderByDescending(r => r.RequestStatus == LeaveRequestStatus.Pending).ThenByDescending(r => r.SubmittedAt).Take(5)];

                // Cần chú ý (D-68 d).
                void Add(bool when, string code2, string severity, string message, string? screen)
                {
                    if (when)
                    {
                        m.ListAlert.Add(new AppDelicare.Model.Model.CustomerAlertModel { Code = code2, Severity = severity, Message = message, Screen = screen });
                    }
                }
                var missed = m.ListUpcomingShift.Where(d => d.WorkDate.Date == today && d.CheckInAt == null && ShiftAssignmentStatus.Open.Contains(d.Status) && d.PlannedStart != null && d.PlannedStart <= now).ToList();
                DateTime week = today.AddDays(-6);
                int outside = db.AttendanceEvent.Count(x => !x.IsDeleted && x.EmployeeId == id && x.IsValid == false && x.EventAt >= week);
                Add(missed.Count > 0, "NOT_CHECKED_IN", "HIGH", string.Format(ConstantHelper.Message.EmployeeAlertNotCheckedIn, string.Join(", ", missed.Select(x => x.ShiftCode))), "hr-attendance");
                Add(outside >= 2, "OUT_OF_RADIUS", "MEDIUM", string.Format(ConstantHelper.Message.EmployeeAlertOutOfRadius, outside), "hr-attendance");
                Add(m.PendingRequestCount > 0, "PENDING_REQUEST", "MEDIUM", string.Format(ConstantHelper.Message.EmployeeAlertPending, m.PendingRequestCount), "hr-leave");
                Add(!m.BotLinked && EmployeeStatus.IsWorking(e.Status), "NO_BOT_LINK", "LOW", ConstantHelper.Message.EmployeeAlertNoBot, null);
                Add(string.IsNullOrWhiteSpace(e.Phone), "NO_PHONE", "LOW", ConstantHelper.Message.EmployeeAlertNoPhone, null);
                return Result<AppDelicare.Model.Model.EmployeeOverviewModel>.Success(m, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetOverview: ", ex.ToString()));
                throw;
            }
        }
    }
}
