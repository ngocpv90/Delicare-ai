using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;

namespace AppDelicare.Services.Interface
{
    /// <summary>BOT-1.2 (D-62 p8): tự phục vụ nhân sự qua bot cho người chỉ có EmployeeId – chỉ dữ liệu của chính mình, ghi dưới system.</summary>
    public interface IBotEmployeeService
    {
        Task<IResults<BotEmployeeShiftModel>> GetMyShifts(BotIdentityRequest request);
        Task<IResult<MyAttendanceModel>> GetToday(BotIdentityRequest request);
        Task<IResult<AttendanceResultModel>> CheckIn(BotEmployeeCheckRequest request);
        Task<IResult<AttendanceResultModel>> CheckOut(BotEmployeeCheckRequest request);
        Task<IResults<BotEmployeeRequestItemModel>> GetMyRequests(BotIdentityRequest request);
        IResults<AppDelicare.Model.Option.ReferenceItem> GetLeaveTypes();
        Task<IResult<LeaveRequestDetailModel>> CreateLeave(BotEmployeeLeaveRequest request);
        Task<IResult<LeaveImpactModel>> PreviewLeave(BotEmployeeLeaveRequest request);
        Task<IResult<ShiftSwapDetailModel>> SwapConsent(BotSwapConsentRequest request);
    }
}
