using Microsoft.AspNetCore.Mvc;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>
    /// BE2.7 (spec 2.1 / 2.2 / 2.5 – Catalog 2, 8, 15, 30): API dùng chung cho mọi màn hình danh sách – không gắn bảng (như DashboardController).
    /// Màn hình chịu quyền phụ thuộc tham số <c>resource</c> / loại phiếu → action <c>[SkipPermission]</c>, service kiểm quyền cũ (UserRole × ObjectMenu)
    /// trên màn của danh sách; không có quyền → HTTP 403 kèm Result (responseCode = 403). Chỉ danh sách trong <c>GetResources</c> (danh sách trắng).
    /// </summary>
    public class CommonController(ICommonService service, IPrintService printService) : BaseController
    {
        private readonly ICommonService _service = service;
        private readonly IPrintService _printService = printService;

        private const int Forbidden = 403;

        /// <summary>Danh sách hỗ trợ thống kê / xuất / lịch sử / đính kèm (chỉ màn người dùng có quyền Xem).</summary>
        [Security.SkipPermission]
        [HttpGet("GetResources")]
        public async Task<IActionResult> GetResources()
        {
            try
            {
                return Ok(await _service.GetResources());
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetResources: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>
        /// MA-1.7 (D-63 k): quy tắc mã của mọi bảng – G1 tự sinh (giao diện chỉ đọc "(tự sinh khi lưu)"), G2 mã người đặt (chữ hoa không dấu), G3 không áp quy tắc.
        /// <paramref name="entity"/> (tùy chọn): tên entity hoặc bảng (vd. <c>Customer</c>, <c>Dishe</c>). Chỉ cần đăng nhập.
        /// </summary>
        [Security.SkipPermission]
        [HttpGet("GetCodeRules")]
        public IActionResult GetCodeRules(string? entity)
        {
            try
            {
                return Ok(_service.GetCodeRules(entity));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetCodeRules: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>Catalog 2: tổng + số bản ghi theo trạng thái (thẻ KPI, chip "Lọc nhanh"). Quyền Xem màn của danh sách.</summary>
        [Security.SkipPermission] // service kiểm quyền Xem trên màn của resource
        [HttpGet("GetStatusSummary")]
        public async Task<IActionResult> GetStatusSummary([FromQuery] CommonListRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                var result = await _service.GetStatusSummary(request);
                return result.ResponseCode == Forbidden ? StatusCode(Forbidden, result) : Ok(result);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetStatusSummary: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>Catalog 8: xuất danh sách (cùng bộ lọc) ra xlsx / csv. Quyền Xuất màn của danh sách. Lỗi → JSON Result.</summary>
        [Security.SkipPermission] // service kiểm quyền Xuất trên màn của resource
        [HttpGet("ExportList")]
        public async Task<IActionResult> ExportList([FromQuery] CommonListRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                var result = await _service.ExportList(request);
                if (result.ResponseCode == Forbidden)
                {
                    return StatusCode(Forbidden, result);
                }
                if (result.ResponseCode != Convert.ToInt32(HttpStatusCode.OK) || result.Data == null)
                {
                    return Ok(result);
                }
                return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportList: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>Catalog 15: tab "Lịch sử xử lý" – AuditChange (+ dòng con) + ApprovalDecision + ComplaintHistory + tệp đính kèm, mới nhất trước; che mật khẩu / bí mật / GPS. Quyền Xem.</summary>
        [Security.SkipPermission] // service kiểm quyền Xem trên màn của resource
        [HttpGet("GetHistory")]
        public async Task<IActionResult> GetHistory(string resource, Guid id)
        {
            try
            {
                var result = await _service.GetHistory(resource, id);
                return result.ResponseCode == Forbidden ? StatusCode(Forbidden, result) : Ok(result);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetHistory: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>
        /// BE3.3 – Catalog 18: chuông cảnh báo trên header (spec 12.1) → [{ code, severity, title, detail, route, count }], chỉ cảnh báo có số &gt; 0.
        /// FE hỏi định kỳ 60 giây; server cache 60 giây theo (người, chi nhánh). Mỗi cảnh báo chỉ trả khi có quyền Xem màn của nó (duyệt = hộp duyệt của chính người dùng).
        /// </summary>
        [Security.SkipPermission] // service lọc theo quyền Xem từng màn của cảnh báo
        [HttpGet("GetAlerts")]
        public async Task<IActionResult> GetAlerts(Guid? branchId)
        {
            try
            {
                return Ok(await _service.GetAlerts(branchId));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAlerts: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE3.3 – Catalog 17: tìm kiếm toàn cục (q ≥ 2 ký tự) → { screens, records } – chỉ màn / loại người dùng được Xem, không trả tên bảng / cột (D-58).</summary>
        [Security.SkipPermission] // service lọc theo quyền Xem từng màn / loại bản ghi
        [HttpGet("GlobalSearch")]
        public async Task<IActionResult> GlobalSearch(string? q)
        {
            try
            {
                return Ok(await _service.GlobalSearch(q));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GlobalSearch: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE3.3 – Catalog 16: tab "Liên quan" của 1 bản ghi → [{ key, title, route, count, items }], mỗi nhóm ≤ 8 dòng. Quyền Xem màn của bản ghi (403); bỏ nhóm không được Xem.</summary>
        [Security.SkipPermission] // service kiểm quyền Xem trên màn của resource và của từng nhóm
        [HttpGet("GetRelated")]
        public async Task<IActionResult> GetRelated(string? resource, Guid id)
        {
            try
            {
                var result = await _service.GetRelated(resource, id);
                return result.ResponseCode == Forbidden ? StatusCode(Forbidden, result) : Ok(result);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetRelated: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BOT-1.7d: nội dung 1 phiếu dạng dữ liệu (bot mở phiếu khi quét QR DOC_ trên phiếu in). type = GOODS_RECEIPT / MATERIAL_ISSUE / STOCK_COUNT / DELIVERY_ORDER / POD / RECONCILIATION (hoặc mã ngắn GR / MI / SC / DO / POD / RC); quyền Xem màn của loại phiếu (service kiểm).</summary>
        [Security.SkipPermission] // service kiểm quyền Xem trên màn của loại phiếu
        [HttpGet("GetPrintDocument")]
        public async Task<IActionResult> GetPrintDocument(string type, Guid id)
        {
            try
            {
                var result = await _printService.GetDocument(AppDelicare.Services.Implement.PrintService.TypeOfQrCode(type) ?? type, id);
                return result.ResponseCode == Forbidden ? StatusCode(Forbidden, result) : Ok(result);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetPrintDocument: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>Catalog 30: in hàng loạt (tối đa 100 phiếu) → 1 tệp pdf (mặc định, mỗi phiếu sang trang mới – BE2.7.1) / xlsx (mỗi phiếu 1 sheet) / docx; quyền In theo từng loại phiếu. JSON vì có danh sách phiếu.</summary>
        [Security.SkipPermission] // service kiểm quyền In trên màn của từng loại phiếu
        [HttpPost("PrintBatch")]
        public async Task<IActionResult> PrintBatch([FromBody] PrintBatchRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                var result = await _printService.PrintBatch(request);
                if (result.ResponseCode == Forbidden)
                {
                    return StatusCode(Forbidden, result);
                }
                if (result.ResponseCode != Convert.ToInt32(HttpStatusCode.OK) || result.Data == null)
                {
                    return Ok(result);
                }
                return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " PrintBatch: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
