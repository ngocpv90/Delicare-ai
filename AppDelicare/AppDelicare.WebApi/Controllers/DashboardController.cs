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
    public class DashboardController(IDashboardService dashboardService, AppDelicare.Services.Implement.IDashboardPlService plService, AppDelicare.Services.Implement.IAmisPlService amisPl, AppDelicare.Services.Implement.IAmisPlReportService plReport) : BaseController
    {
        private readonly AppDelicare.Services.Implement.IAmisPlReportService _plReport = plReport;

        /// <summary>RPT-1.3 (D-140 f, i): báo cáo KQKD Tháng | Lũy kế năm, theo quý, chuỗi 12 tháng (Chính thức / Tạm tính), cơ cấu giá vốn.</summary>
        [HttpGet("GetPlReport")]
        [Security.Permission("dash-pl", PermissionAction.View)]
        public Task<IActionResult> GetPlReport([FromQuery] string? month) => Run(nameof(GetPlReport), () => _plReport.GetReport(month));

        /// <summary>RPT-1.4: xuất tab Kết quả kinh doanh – format xlsx (mặc định) / pdf; quyền Xuất mục dash-pl.</summary>
        [HttpGet("ExportPl")]
        [Security.Permission("dash-pl", PermissionAction.Export)]
        public async Task<IActionResult> ExportPl([FromQuery] string? month, [FromQuery] string? format)
        {
            try
            {
                var r = await _plReport.Export(month, format);
                return r.ResponseCode == 200 && r.Data != null ? File(r.Data.Content, r.Data.ContentType, r.Data.FileName) : Ok(r);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "] ExportPl: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
        private readonly AppDelicare.Services.Implement.IAmisPlService _amisPl = amisPl;

        private async Task<IActionResult> Run<T>(string name, Func<Task<T>> call)
        {
            try
            {
                return Ok(await call());
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "] ", name, ": ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>RPT-1.2 (D-140 d): nhập sổ AMIS (sổ chi tiết 511, 621, 622, 627, 641, 642, 711, 811 + B01-DN, nhiều tệp một lần) – quyền Nhập mục dash-pl (Kế toán).</summary>
        [HttpPost("ImportAmis")]
        [RequestSizeLimit(50_000_000)]
        [Security.Permission("dash-pl", PermissionAction.Import)]
        public Task<IActionResult> ImportAmis([FromForm] string? month, [FromForm] List<IFormFile>? files) => Run(nameof(ImportAmis), async () =>
        {
            var list = (files ?? []).Where(f => f.Length > 0).ToList();
            var streams = new List<(string, Stream)>();
            try
            {
                foreach (var f in list)
                {
                    var ms = new MemoryStream();
                    await f.CopyToAsync(ms);
                    ms.Position = 0;
                    streams.Add((f.FileName, ms));
                }
                return await _amisPl.ImportExcel(string.IsNullOrWhiteSpace(month) ? null : month, streams);
            }
            finally
            {
                streams.ForEach(x => x.Item2.Dispose());
            }
        });

        /// <summary>RPT-1.2 / 1.3: nhập dòng sổ đã chuẩn hóa (công cụ tools/rpt1 nạp lịch sử) – cùng quy tắc với nhập Excel.</summary>
        [HttpPost("ImportAmisLines")]
        [RequestSizeLimit(50_000_000)]
        [Security.Permission("dash-pl", PermissionAction.Import)]
        public Task<IActionResult> ImportAmisLines([FromBody] AppDelicare.Services.Implement.AmisPlImportRequest request) => Run(nameof(ImportAmisLines), () => _amisPl.Import(request ?? new()));

        /// <summary>RPT-1.2: số AMIS đã nhập của tháng + khoản mục + dòng chưa phân loại.</summary>
        [HttpGet("GetAmisMonth")]
        [Security.Permission("dash-pl", PermissionAction.View)]
        public Task<IActionResult> GetAmisMonth([FromQuery] string? month) => Run(nameof(GetAmisMonth), () => _amisPl.GetMonth(month));

        /// <summary>RPT-1.2 (D-140 d): bảng quy tắc phân loại khoản mục.</summary>
        [HttpGet("GetPlRules")]
        [Security.Permission("dash-pl", PermissionAction.View)]
        public Task<IActionResult> GetPlRules() => Run(nameof(GetPlRules), _amisPl.GetRules);

        [HttpPost("SavePlRules")]
        [Security.Permission("dash-pl", PermissionAction.Edit)]
        public Task<IActionResult> SavePlRules([FromBody] List<AppDelicare.Services.Implement.AmisPlRule> rules) => Run(nameof(SavePlRules), () => _amisPl.SaveRules(rules ?? []));
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
