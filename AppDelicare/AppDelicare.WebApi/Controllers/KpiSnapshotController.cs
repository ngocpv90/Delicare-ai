using Microsoft.AspNetCore.Mvc;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using AppDelicare.WebApi.Security;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>
    /// BE3.4 (spec 11.7, Catalog 222–224): KPI ngày / tháng (đọc snapshot) + nút làm mới bấm tay (D-57 a – job để sau, D-45 / D-48).
    /// Controller không gắn bảng (như DashboardController): quyền gắn rõ từng action – GetDaily = Xem <c>#/kpi-daily</c>, GetMonthly = Xem <c>#/kpi-monthly</c>,
    /// Refresh = chỉ admin (spec: "admin / quyền Làm mới"; không có cột quyền riêng – PROPOSED D-57 z).
    /// </summary>
    public class KpiSnapshotController(IKpiSnapshotService snapshotService, IKpiCalculationService calculationService) : BaseController
    {
        private readonly IKpiSnapshotService _snapshotService = snapshotService;
        private readonly IKpiCalculationService _calculationService = calculationService;

        /// <summary>Catalog 222: KPI ngày – from / to (trống = 30 ngày gần nhất, tối đa 366), branchId (trống = toàn công ty), domainCode.</summary>
        [Permission("kpi-daily", Services.Interface.PermissionAction.View)]
        [HttpGet("GetDaily")]
        public async Task<IActionResult> GetDaily([FromQuery] KpiDailyRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _snapshotService.GetDaily(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDaily: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>Catalog 223: KPI tháng – fromMonth / toMonth "yyyy-MM" (trống = 12 tháng gần nhất, tối đa 36), branchId, domainCode.</summary>
        // UI-6.28 (D-109): "Chỉ số theo ngày" + "Chỉ số theo tháng" gộp thành "Chỉ số kinh doanh" (kpi-daily) – mục kpi-monthly ẩn.
        [Permission("kpi-daily", Services.Interface.PermissionAction.View)]
        [HttpGet("GetMonthly")]
        public async Task<IActionResult> GetMonthly([FromQuery] KpiMonthlyRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _snapshotService.GetMonthly(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMonthly: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>
        /// Catalog 224: làm mới KPI bấm tay – jobCode KPI_DAILY (date, trống = hôm nay) / KPI_MONTHLY (month "yyyy-MM", trống = tháng này), branchId?.
        /// Khóa theo jobCode (đang chạy → 400), ghi AnalyticsRefreshLog Running → Succeeded / Failed, upsert snapshot (chạy lại không nhân đôi).
        /// </summary>
        [AdminOnly]
        [HttpPost("Refresh")]
        public async Task<IActionResult> Refresh([FromForm] KpiRefreshRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _calculationService.Refresh(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Refresh: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
