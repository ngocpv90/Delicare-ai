#nullable enable
using Microsoft.AspNetCore.Mvc;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>UI-6.39 (D-120): "Nghỉ & đổi ca" – danh sách gộp đơn nghỉ + đổi / nhường ca, chỉ số, lịch nghỉ, xem trước hệ quả đơn nghỉ.</summary>
    public class HrRequestController(IHrRequestService service) : BaseController
    {
        /// <summary>Danh sách gộp – người không có quyền xem màn chỉ thấy đơn của mình (service lọc).</summary>
        [Security.SkipPermission]
        [HttpGet("GetList")]
        public Task<IActionResult> GetList([FromQuery] HrRequestListRequest request) => Run(nameof(GetList), () => service.GetList(request));

        [Security.Permission("hr-leave", PermissionAction.View, OrScreens = ["hr-swap"])]
        [HttpGet("GetSummary")]
        public Task<IActionResult> GetSummary(Guid? organizationUnitId) => Run(nameof(GetSummary), () => service.GetSummary(organizationUnitId));

        [Security.Permission("hr-leave", PermissionAction.View, OrScreens = ["hr-swap"])]
        [HttpGet("GetCalendar")]
        public Task<IActionResult> GetCalendar(DateTime? from, DateTime? to, Guid? organizationUnitId) => Run(nameof(GetCalendar), () => service.GetCalendar(from, to, organizationUnitId));

        /// <summary>Xem trước đơn nghỉ: phép năm còn / sau, ca bị bỏ, người cùng tổ nghỉ trùng, tổ còn x / y theo ca.</summary>
        [Security.SkipPermission]
        [HttpGet("PreviewLeave")]
        public Task<IActionResult> PreviewLeave([FromQuery] LeavePreviewRequest request) => Run(nameof(PreviewLeave), () => service.PreviewLeave(request));

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
    }
}
