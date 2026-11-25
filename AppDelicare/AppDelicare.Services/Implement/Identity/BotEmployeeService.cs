using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AppDelicare.Services.Implement.Identity
{
    /// <summary>
    /// BOT-1.2 (D-62 p8, BOT_TELEGRAM §2.6): API hẹp cho người CHỈ có EmployeeId (không tài khoản web → không JWT). EmployeeId luôn lấy từ định danh bot
    /// (không nhận từ request) → chỉ dữ liệu / thao tác của chính mình: lịch ca 7 ngày, ca hôm nay + vào / ra ca (quy tắc tự chấm của AttendanceEventService),
    /// đơn nghỉ / đổi ca 60 ngày. Ghi dưới tài khoản <c>system</c>, Note ghi nguồn "Telegram – &lt;tên&gt;".
    /// </summary>
    public class BotEmployeeService(IUnitOfWork unitOfWork, ICurrentActor currentActor, IConfiguration configuration, IClock clock,
        IAttendanceEventService attendance, ILeaveRequestService leaves, IShiftSwapRequestService swaps, Microsoft.Extensions.Options.IOptionsMonitor<ReferenceListsOptions> references) : IBotEmployeeService
    {
        public async Task<IResults<BotEmployeeShiftModel>> GetMyShifts(BotIdentityRequest request)
        {
            var (employee, error) = await EmployeeAsync(request);
            if (employee == null)
            {
                return Results<BotEmployeeShiftModel>.Fail(error!);
            }
            return Results<BotEmployeeShiftModel>.Success(await ShiftsAsync(unitOfWork, clock, employee.Id, 7), ConstantHelper.Message.GetAllSuccessfully);
        }

        /// <summary>BOT-1.5a: ca từ hôm nay tới hôm nay + <paramref name="days"/> (dùng chung bot nhân viên + ShiftAssignment/GetMine của tài khoản web).</summary>
        public static async Task<List<BotEmployeeShiftModel>> ShiftsAsync(IUnitOfWork unitOfWork, IClock clock, Guid employeeId, int days)
        {
            DateTime today = clock.Today, to = today.AddDays(Math.Clamp(days, 1, 31));
            var db = unitOfWork.DbContext;
            var rows = await (from a in db.ShiftAssignment.AsNoTracking()
                              join s in db.Shift.AsNoTracking() on a.ShiftId equals s.Id into sj
                              from s in sj.DefaultIfEmpty()
                              where !a.IsDeleted && a.EmployeeId == employeeId && a.WorkDate >= today && a.WorkDate < to
                              orderby a.WorkDate, a.PlannedStart
                              select new BotEmployeeShiftModel
                              {
                                  AssignmentId = a.Id, WorkDate = a.WorkDate, ShiftCode = s.Code, ShiftName = s.Name,
                                  PlannedStart = a.PlannedStart, PlannedEnd = a.PlannedEnd, AssignmentStatus = a.AssignmentStatus,
                              }).ToListAsync();
            return rows;
        }

        /// <summary>BOT-1.5a: loại nghỉ (ReferenceLists:LeaveType) cho nút chọn trên bot.</summary>
        public IResults<ReferenceItem> GetLeaveTypes()
            => Results<ReferenceItem>.Success(references.CurrentValue.Lists.TryGetValue("LeaveType", out var list) ? list : [], ConstantHelper.Message.GetAllSuccessfully);

        /// <summary>BOT-1.5a: xin nghỉ qua bot cho người chỉ có EmployeeId (quy tắc LeaveRequestService.Create, ghi dưới system).</summary>
        /// <summary>UI-6.39 (D-120): người nhận đồng ý / từ chối đơn đổi / nhường ca qua bot.</summary>
        public async Task<IResult<ShiftSwapDetailModel>> SwapConsent(BotSwapConsentRequest request)
        {
            var (employee, error) = await EmployeeAsync(request);
            if (employee == null)
            {
                return Result<ShiftSwapDetailModel>.Fail(error!);
            }
            if (!Guid.TryParse(configuration[$"{SystemUsersOptions.Section}:{nameof(SystemUsersOptions.JobUserId)}"], out Guid system) || system == Guid.Empty)
            {
                return Result<ShiftSwapDetailModel>.Fail(ConstantHelper.Message.JobUserMissing);
            }
            using (currentActor.BeginScope(ActorInfo.ForSystem(system)))
            {
                return await swaps.ConsentForEmployee(employee.Id, new ShiftSwapConsentRequest { Id = request.SwapId, Accept = request.Accept, Note = request.Note });
            }
        }

        public async Task<IResult<LeaveRequestDetailModel>> CreateLeave(BotEmployeeLeaveRequest request)
        {
            var (employee, error) = await EmployeeAsync(request);
            if (employee == null)
            {
                return Result<LeaveRequestDetailModel>.Fail(error!);
            }
            if (!Guid.TryParse(configuration[$"{SystemUsersOptions.Section}:{nameof(SystemUsersOptions.JobUserId)}"], out Guid system) || system == Guid.Empty)
            {
                return Result<LeaveRequestDetailModel>.Fail(ConstantHelper.Message.JobUserMissing);
            }
            using (currentActor.BeginScope(ActorInfo.ForSystem(system)))
            {
                return await leaves.CreateForEmployee(employee.Id, new LeaveCreateRequest
                {
                    FromDate = request.FromDate ?? clock.Today, ToDate = request.ToDate ?? request.FromDate ?? clock.Today, LeaveTypeCode = request.LeaveTypeCode,
                    Reason = MessagingRules.Truncate("[Telegram] " + request.Reason, 1000),
                });
            }
        }

        /// <summary>UI-6.49 (1): xem trước đơn nghỉ qua bot (phép năm còn / sau, ca bị bỏ) cho người chỉ có EmployeeId – cùng công thức HrRequest/PreviewLeave.</summary>
        public async Task<IResult<LeaveImpactModel>> PreviewLeave(BotEmployeeLeaveRequest request)
        {
            var (employee, error) = await EmployeeAsync(request);
            if (employee == null)
            {
                return Result<LeaveImpactModel>.Fail(error!);
            }
            DateTime from = (request.FromDate ?? clock.Today).Date, to = (request.ToDate ?? request.FromDate ?? clock.Today).Date;
            var hr = (configuration.GetSection(BusinessRulesOptions.Section).Get<BusinessRulesOptions>() ?? new BusinessRulesOptions()).Hr;
            if (to < from)
            {
                return Result<LeaveImpactModel>.Fail(string.Format(ConstantHelper.Message.HrDateRangeInvalid, hr.LeaveMaxDays));
            }
            return Result<LeaveImpactModel>.Success(await HrRequestService.LeaveImpactAsync(unitOfWork.DbContext, hr, employee.Id, from, to, request.LeaveTypeCode, null), ConstantHelper.Message.GetAllSuccessfully);
        }

        public async Task<IResult<MyAttendanceModel>> GetToday(BotIdentityRequest request)
        {
            var (employee, error) = await EmployeeAsync(request);
            return employee == null ? Result<MyAttendanceModel>.Fail(error!) : await attendance.GetForEmployee(employee.Id);
        }

        public Task<IResult<AttendanceResultModel>> CheckIn(BotEmployeeCheckRequest request) => CheckAsync(request, true);

        public Task<IResult<AttendanceResultModel>> CheckOut(BotEmployeeCheckRequest request) => CheckAsync(request, false);

        public async Task<IResults<BotEmployeeRequestItemModel>> GetMyRequests(BotIdentityRequest request)
        {
            var (employee, error) = await EmployeeAsync(request);
            if (employee == null)
            {
                return Results<BotEmployeeRequestItemModel>.Fail(error!);
            }
            var db = unitOfWork.DbContext;
            DateTime since = clock.Today.AddDays(-60);
            var leaves = await db.LeaveRequest.AsNoTracking().Where(l => !l.IsDeleted && l.EmployeeId == employee.Id && l.CreatedDate >= since)
                .Select(l => new BotEmployeeRequestItemModel { Id = l.Id, Kind = "LEAVE", FromDate = l.FromDate, ToDate = l.ToDate, Status = l.RequestStatus, CreatedDate = l.CreatedDate })
                .ToListAsync();
            var swaps = await db.ShiftSwapRequest.AsNoTracking().Where(w => !w.IsDeleted && w.RequesterEmployeeId == employee.Id && w.CreatedDate >= since)
                .Select(w => new BotEmployeeRequestItemModel { Id = w.Id, Kind = "SWAP", Status = w.RequestStatus, CreatedDate = w.CreatedDate })
                .ToListAsync();
            return Results<BotEmployeeRequestItemModel>.Success([.. leaves.Concat(swaps).OrderByDescending(x => x.CreatedDate)], ConstantHelper.Message.GetAllSuccessfully);
        }

        private async Task<IResult<AttendanceResultModel>> CheckAsync(BotEmployeeCheckRequest request, bool checkIn)
        {
            var (employee, error) = await EmployeeAsync(request);
            if (employee == null)
            {
                return Result<AttendanceResultModel>.Fail(error!);
            }
            if (!Guid.TryParse(configuration[$"{SystemUsersOptions.Section}:{nameof(SystemUsersOptions.JobUserId)}"], out Guid system) || system == Guid.Empty)
            {
                return Result<AttendanceResultModel>.Fail(ConstantHelper.Message.JobUserMissing);
            }
            string who = request.DisplayName ?? employee.FullName ?? employee.Code ?? employee.Id.ToString();
            using (currentActor.BeginScope(ActorInfo.ForSystem(system)))
            {
                return await attendance.CheckForEmployee(employee.Id, new AttendanceCheckRequest
                {
                    Latitude = request.Latitude, Longitude = request.Longitude, SourceChannel = request.SourceChannel,
                    WorkstationCode = request.WorkstationCode, ExternalEventId = request.ExternalEventId,
                }, checkIn, MessagingRules.Truncate("Nguồn: Telegram – " + who, 200)!);
            }
        }

        /// <summary>Định danh dùng được trên bot + có EmployeeId → nhân viên đang làm việc.</summary>
        private async Task<(Employee? Employee, string? Error)> EmployeeAsync(BotIdentityRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.BotCode) || string.IsNullOrWhiteSpace(request.ExternalUserId))
            {
                return (null, ConstantHelper.Message.BotIdentityInvalid);
            }
            var db = unitOfWork.DbContext;
            var identity = await TelegramLinkService.FindIdentityAsync(db, request.BotCode.Trim().ToUpperInvariant(), request.ExternalUserId);
            if (identity == null || !MessagingCode.Identity.IsUsable(identity.IdentityStatus))
            {
                return (null, ConstantHelper.Message.BotNotLinked);
            }
            if (identity.EmployeeId == null)
            {
                return (null, ConstantHelper.Message.BotEmployeeOnly);
            }
            var employee = await db.Employee.AsNoTracking().FirstOrDefaultAsync(e => e.Id == identity.EmployeeId && !e.IsDeleted);
            return employee == null || !EmployeeStatus.IsWorking(employee.Status) ? (null, ConstantHelper.Message.HrEmployeeNotFound) : (employee, null);
        }
    }
}
