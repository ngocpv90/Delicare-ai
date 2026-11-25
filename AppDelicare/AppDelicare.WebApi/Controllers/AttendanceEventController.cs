using AppDelicare.Model.Model;
// Sinh bởi tools/AppDelicare.CodeGen (T05), nay là file của dev (BE2.6: action nghiệp vụ nhân sự) – CodeGen không ghi đè.
#nullable enable
using Microsoft.AspNetCore.Mvc;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>Sổ chỉ ghi thêm: không có Update / Delete (CLAUDE.md 3.3, T08).</summary>
    public class AttendanceEventController(IAttendanceEventService service) : BaseController
    {
        private readonly IAttendanceEventService _service = service;

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] AttendanceEventSearchRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.GetAll(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAll: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        [HttpPost("Insert")]
        public async Task<IActionResult> Insert([FromForm] AttendanceEventRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Save(request, ConstantHelper.TypeSave.Insert));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Insert: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        [HttpGet("GetById")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                return Ok(await _service.GetById(id));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetById: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        [HttpGet("GetAllData")]
        public IActionResult GetAllData()
        {
            try
            {
                return Ok(_service.GetAllData());
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAllData: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.6 (spec 11.2, Catalog 205): vào ca – GPS theo bán kính trạm bếp / QR / máy / chấm hộ.</summary>
        [Security.SkipPermission] // BE2.6: tự phục vụ – service kiểm (chính nhân viên của tài khoản, hoặc quyền Thêm của màn khi làm cho người khác)
        [HttpPost("CheckIn")]
        public async Task<IActionResult> CheckIn([FromForm] AttendanceCheckRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.CheckIn(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CheckIn: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.6 (Catalog 205): ra ca.</summary>
        [Security.SkipPermission] // BE2.6: tự phục vụ – service kiểm (chính nhân viên của tài khoản, hoặc quyền Thêm của màn khi làm cho người khác)
        [HttpPost("CheckOut")]
        public async Task<IActionResult> CheckOut([FromForm] AttendanceCheckRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.CheckOut(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CheckOut: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.6 (Catalog 206): bảng công theo nhân viên (danh sách, phân trang).</summary>
        // ------------------------------------------------------------ UI-6.38 (D-119)

        /// <summary>Bổ sung giờ thực tế (quên vào / ra ca) – trong khung ca, ≤ Hr:SupplementMaxDays ngày; ghi thêm 1 lần chấm PROXY.</summary>
        [HttpPost("Supplement")]
        [Security.Permission("hr-attendance", PermissionAction.Add)]
        public Task<IActionResult> Supplement([FromBody] AttendanceSupplementRequest request) => Run(nameof(Supplement), () => _service.Supplement(request));

        /// <summary>Xác nhận hợp lệ lần chấm ngoài bán kính (lý do bắt buộc).</summary>
        [HttpPost("ConfirmValid")]
        [Security.Permission("hr-attendance", PermissionAction.Edit)]
        public Task<IActionResult> ConfirmValid([FromBody] AttendanceConfirmValidRequest request) => Run(nameof(ConfirmValid), () => _service.ConfirmValid(request));

        /// <summary>Báo vắng một ca chưa vào (tab Hôm nay).</summary>
        [HttpPost("ReportAbsent")]
        [Security.Permission("hr-attendance", PermissionAction.Edit)]
        public Task<IActionResult> ReportAbsent([FromBody] AttendanceReportAbsentRequest request) => Run(nameof(ReportAbsent), () => _service.ReportAbsent(request));

        [HttpGet("GetDayBoard")]
        [Security.Permission("hr-attendance", PermissionAction.View)]
        public Task<IActionResult> GetDayBoard(DateTime? date, Guid? shiftId, Guid? organizationUnitId) => Run(nameof(GetDayBoard), () => _service.GetDayBoard(date, shiftId, organizationUnitId));

        [HttpGet("GetMonthGrid")]
        [Security.Permission("hr-attendance", PermissionAction.View)]
        public Task<IActionResult> GetMonthGrid(string? month, Guid? organizationUnitId) => Run(nameof(GetMonthGrid), () => _service.GetMonthGrid(month, organizationUnitId));

        /// <summary>Xuất bảng công tháng (.xlsx).</summary>
        [HttpGet("ExportTimesheet")]
        [Security.Permission("hr-attendance", PermissionAction.Export, OrScreens = ["hr-attendance"])]
        public async Task<IActionResult> ExportTimesheet(string? month, Guid? organizationUnitId)
        {
            try
            {
                var result = await _service.ExportTimesheet(month, organizationUnitId);
                if (result.ResponseCode != Convert.ToInt32(HttpStatusCode.OK) || result.Data == null)
                {
                    return Ok(result);
                }
                return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportTimesheet: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        private async Task<IActionResult> Run<T>(string action, Func<Task<T>> run)
        {
            try
            {
                return Ok(await run());
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "] ", action, ": ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        [HttpGet("GetTimesheet")]
        public async Task<IActionResult> GetTimesheet([FromQuery] TimesheetRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.GetTimesheet(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetTimesheet: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.6 (Catalog 206): bảng công chi tiết theo ca.</summary>
        [HttpGet("GetTimesheetDetail")]
        public async Task<IActionResult> GetTimesheetDetail([FromQuery] TimesheetRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.GetTimesheetDetail(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetTimesheetDetail: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.6: ca hôm nay của nhân viên đang đăng nhập + vào / ra ca được không (màn chấm công tự phục vụ).</summary>
        [Security.SkipPermission] // BE2.6: chỉ dữ liệu của chính nhân viên gắn với tài khoản
        [HttpGet("GetMine")]
        public async Task<IActionResult> GetMine()
        {
            try
            {
                return Ok(await _service.GetMine());
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMine: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
