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
