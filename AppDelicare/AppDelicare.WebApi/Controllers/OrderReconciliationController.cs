using Microsoft.AspNetCore.Mvc;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    public class OrderReconciliationController(IOrderReconciliationService service, IPrintService printService) : BaseController
    {
        private readonly IOrderReconciliationService _service = service;
        private readonly IPrintService _printService = printService;

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] OrderReconciliationSearchRequest request)
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
        public async Task<IActionResult> Insert([FromForm] OrderReconciliationRequest request)
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

        [HttpPut("Update")]
        public async Task<IActionResult> Update([FromForm] OrderReconciliationRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Save(request, ConstantHelper.TypeSave.Update));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Update: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        [HttpDelete("Delete")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                OrderReconciliationRequest request = new()
                {
                    Id = id
                };
                return Ok(await _service.Save(request, ConstantHelper.TypeSave.Delete));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Delete: ", ex.ToString()));
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

        /// <summary>T19: đơn Đã giao chờ đối soát (lọc ngày ăn / khách).</summary>
        [HttpGet("GetPendingOrders")]
        public async Task<IActionResult> GetPendingOrders([FromQuery] ReconciliationPendingRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.GetPendingOrders(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetPendingOrders: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T19: chi tiết đối soát – đặt / giao / nhận / phục vụ / trả về / hủy, cơ sở tính tiền.</summary>
        [HttpGet("GetDetail")]
        public async Task<IActionResult> GetDetail(Guid id)
        {
            try
            {
                return Ok(await _service.GetDetail(id));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T19: đối soát của 1 đơn hàng.</summary>
        [HttpGet("GetByOrder")]
        public async Task<IActionResult> GetByOrder(Guid orderId)
        {
            try
            {
                return Ok(await _service.GetByOrder(orderId));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetByOrder: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T19: đối soát đơn Đã giao – suất thực tế phục vụ (khách xác nhận) → Khớp / Chênh lệch, giá trị tính tiền.</summary>
        [HttpPost("Reconcile")]
        public async Task<IActionResult> Reconcile([FromForm] OrderReconcileRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Reconcile(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Reconcile: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T19: đóng đối soát → đơn hàng Đã đối soát; chênh lệch &gt; ngưỡng cần ghi chú.</summary>
        [HttpPost("Close")]
        public async Task<IActionResult> Close([FromForm] ReconciliationCloseRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Close(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Close: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.7 (Catalog 29): in biên bản đối soát (không in khi còn Mở) – PDF (mặc định, BE2.7.1 – cấu hình BusinessRules:Common:PrintDefaultFormat, D-56 k), format=xlsx (Excel) hoặc docx (Word). Quyền In #/crm-reconciliation (PermissionConvention). Lỗi / chưa đủ điều kiện → JSON Result.</summary>
        [HttpGet("Print")]
        public async Task<IActionResult> Print(Guid id, string? format = null)
        {
            try
            {
                var result = await _printService.Print("RECONCILIATION", id, format);
                if (result.ResponseCode != Convert.ToInt32(HttpStatusCode.OK) || result.Data == null)
                {
                    return Ok(result);
                }
                return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Print: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE3.1 (Catalog 100): danh sách đối soát theo ngày ăn (from / to – mặc định 30 ngày, status, customerId, searchText, page) + <c>chart</c> Đặt – Giao – Nhận theo ngày + <c>statusCounts</c>.</summary>
        [HttpGet("GetList")]
        public async Task<IActionResult> GetList([FromQuery] ReconciliationListRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.GetList(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
