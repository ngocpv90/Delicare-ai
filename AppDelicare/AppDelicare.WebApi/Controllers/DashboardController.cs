using Microsoft.AspNetCore.Mvc;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>
    /// Dashboard (không gắn bảng – màn <c>#/dash-exec</c> theo PermissionConvention.ControllerScreens). BE3.5 (Catalog 231, 232 – spec 12.1, 12.2):
    /// dashboard điều hành + hành trình đơn hàng. Chỉ đọc.
    /// </summary>
    public class DashboardController(IDashboardService dashboardService, AppDelicare.Services.Implement.IDashboardPlService plService) : BaseController
    {
        private readonly AppDelicare.Services.Implement.IDashboardPlService _plService = plService;

        /// <summary>RPT-1.1 (D-140 a): tab "Kết quả kinh doanh" (dash-exec?tab=pl) – mục quyền ẩn dash-pl (CEO, COO, FIN-IT, FIN-ACC); người khác 403.</summary>
        [HttpGet("GetPl")]
        [Security.Permission("dash-pl", PermissionAction.View)]
        public async Task<IActionResult> GetPl([FromQuery] string? month, [FromQuery] Guid? branchId)
        {
            try
            {
                return Ok(await _plService.GetPl(month, branchId));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetPl: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(System.Net.HttpStatusCode.InternalServerError), AppDelicare.Model.Helper.ConstantHelper.Message.InternalServer);
            }
        }
        private readonly IDashboardService _dashboardService = dashboardService;

        private const int Forbidden = 403;

        /// <summary>
        /// BE3.5 – Catalog 231: dashboard điều hành của ngày (trống = hôm nay), lọc ca / chi nhánh (gồm đơn vị con) → kpi, chart.meals7d, shiftProgress, kpiSnapshot,
        /// orders (6 đơn đầu + trạng thái giao), alerts (= Common/GetAlerts của người đang đăng nhập). Quyền Xem <c>#/dash-exec</c> (tự gắn theo tên action Get*).
        /// </summary>
        [HttpGet("GetExecutive")]
        public async Task<IActionResult> GetExecutive(DateTime? date, Guid? shiftId, Guid? branchId)
        {
            try
            {
                return Ok(await _dashboardService.GetExecutive(date, shiftId, branchId));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetExecutive: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>
        /// BE3.5 – Catalog 232: hành trình 1 đơn hàng (Đơn → Kế hoạch SX → Lệnh SX / mẻ → Đóng gói → Đơn giao / chuyến → POD → Đối soát → Phản ánh).
        /// Quyền Xem 1 trong các màn <c>#/flow-e2e</c>, <c>#/dash-exec</c>, <c>#/crm-order</c> (service kiểm – không có → 403); chứng từ chỉ trả khi được Xem màn của chứng từ.
        /// </summary>
        [Security.SkipPermission] // service kiểm quyền Xem (flow-e2e / dash-exec / crm-order) và quyền Xem màn của từng chứng từ
        [HttpGet("GetOrderJourney")]
        public async Task<IActionResult> GetOrderJourney(Guid orderId)
        {
            try
            {
                var result = await _dashboardService.GetOrderJourney(orderId);
                return result.ResponseCode == Forbidden ? StatusCode(Forbidden, result) : Ok(result);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetOrderJourney: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        //[HttpGet("GetCount")]
        //public async Task<IActionResult> GetCount()
        //{
        //    try
        //    {
        //        return Ok(await _dashboardService.GetCount());
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Error(string.Concat("[", GetType().Name, "]", " GetCount: ", ex.ToString()));
        //        return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
        //    }
        //}

        //[HttpGet("GetListChartOptionsBar")]
        //public async Task<IActionResult> GetListChartOptionsBar()
        //{
        //    try
        //    {
        //        return Ok(await _dashboardService.GetListChartOptionsBar());
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Error(string.Concat("[", GetType().Name, "]", " GetListChartOptionsBar: ", ex.ToString()));
        //        return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
        //    }
        //}

        //[HttpGet("GetListChartOptionsPolarArea")]
        //public async Task<IActionResult> GetListChartOptionsPolarArea()
        //{
        //    try
        //    {
        //        return Ok(await _dashboardService.GetListChartOptionsPolarArea());
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Error(string.Concat("[", GetType().Name, "]", " GetListChartOptionsPolarArea: ", ex.ToString()));
        //        return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
        //    }
        //}
    }
}
