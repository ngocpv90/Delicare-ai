// Sinh bởi tools/AppDelicare.CodeGen (T05), nay là file của dev (BE2.6) – CodeGen không ghi đè.
#nullable enable
using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface IAttendanceEventService
    {
        public Task<IResults<AttendanceEventViewModel>> GetAll(AttendanceEventSearchRequest request);
        public Task<IResult<bool>> Save(AttendanceEventRequest request, string typeSave);
        public IResults<AttendanceEvent> GetAllData();
        public Task<IResult<AttendanceEvent>> GetById(Guid id);

        // BE2.6 (spec 11.2, Catalog 205, 206) – chấm công + bảng công.
        /// <summary>Vào ca (GPS / QR / máy / chấm hộ).</summary>
        public Task<IResult<AttendanceResultModel>> CheckIn(AttendanceCheckRequest request);
        /// <summary>Ra ca.</summary>
        public Task<IResult<AttendanceResultModel>> CheckOut(AttendanceCheckRequest request);
        /// <summary>Bảng công theo nhân viên (danh sách, phân trang).</summary>
        public Task<IResults<TimesheetRowModel>> GetTimesheet(TimesheetRequest request);
        /// <summary>Bảng công chi tiết theo ca.</summary>
        public Task<IResults<TimesheetDetailModel>> GetTimesheetDetail(TimesheetRequest request);
        /// <summary>Ca hôm nay của nhân viên đang đăng nhập + vào / ra ca được không.</summary>
        public Task<IResult<MyAttendanceModel>> GetMine();
        /// <summary>BOT-1.2: ca hôm nay của nhân viên lấy từ định danh bot.</summary>
        public Task<IResult<MyAttendanceModel>> GetForEmployee(Guid employeeId);
        /// <summary>BOT-1.2: chấm công qua bot cho người chỉ có EmployeeId (quy tắc tự chấm, Note ghi nguồn).</summary>
        public Task<IResult<AttendanceResultModel>> CheckForEmployee(Guid employeeId, AttendanceCheckRequest request, bool checkIn, string sourceNote);
    }
}
