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
    public class PurchaseOrderController(IPurchaseOrderService service) : BaseController
    {
        private readonly IPurchaseOrderService _service = service;

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] PurchaseOrderSearchRequest request)
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

        /// <summary>UI-6.18 D (D-99): dòng đề nghị đã duyệt cần chọn NCC (≤ 3 thẻ NCC) + PO chưa gửi + 4 chỉ số. Quyền Xem.</summary>
        [HttpGet("GetSupplierPickBoard")]
        public async Task<IActionResult> GetSupplierPickBoard([FromQuery] AppDelicare.Model.Model.SupplierPickBoardRequest request)
        {
            try
            {
                return Ok(await _service.GetSupplierPickBoard(request ?? new()));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetSupplierPickBoard: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.18 D: lập PO từ các dòng đã chọn NCC (mỗi NCC 1 PO; đủ 3 điều kiện tự duyệt, không thì gửi duyệt). JSON. Quyền Thêm.</summary>
        [HttpPost("CreateFromPick")]
        public async Task<IActionResult> CreateFromPick([FromBody] AppDelicare.Model.Request.Custom.PurchasePickCreateRequest request)
        {
            try
            {
                return null == request ? BadRequest(ConstantHelper.Message.ModelStateMessage) : Ok(await _service.CreateFromPick(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CreateFromPick: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.18 D: gửi nhiều PO Đã duyệt cho NCC một lần. JSON. Quyền Sửa.</summary>
        [HttpPost("SendMany")]
        public async Task<IActionResult> SendMany([FromBody] AppDelicare.Model.Request.Custom.PurchaseOrderSendManyRequest request)
        {
            try
            {
                return null == request ? BadRequest(ConstantHelper.Message.ModelStateMessage) : Ok(await _service.SendMany(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SendMany: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        [HttpPost("Insert")]
        public async Task<IActionResult> Insert([FromForm] PurchaseOrderRequest request)
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
        public async Task<IActionResult> Update([FromForm] PurchaseOrderRequest request)
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
                PurchaseOrderRequest request = new()
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

        /// <summary>UI-6 (D-80): trang chi tiết toàn màn – quyền Xem scm-po (PermissionConvention: Get* = Xem).</summary>
        [HttpGet("GetOverview")]
        public async Task<IActionResult> GetOverview(Guid id)
        {
            try
            {
                return Ok(await _service.GetOverview(id));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetOverview: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T15: chi tiết PO – dòng + đã nhận / đang chờ / còn lại, phiếu nhập, yêu cầu duyệt đang chờ, thao tác được phép.</summary>
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

        /// <summary>T15: sửa toàn bộ dòng PO Nháp, tính lại tổng tiền. JSON body vì có danh sách dòng lồng nhau.</summary>
        [HttpPut("UpdateLines")]
        public async Task<IActionResult> UpdateLines([FromBody] PurchaseOrderUpdateLinesRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.UpdateLines(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " UpdateLines: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T15: Nháp → Chờ duyệt (ApprovalRequest PURCHASE_ORDER, 1 cấp) hoặc tự duyệt dưới ngưỡng cấu hình.</summary>
        [HttpPost("Submit")]
        public async Task<IActionResult> Submit([FromForm] PurchaseActionRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Submit(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Submit: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>
        /// T15: duyệt PO qua engine T12 – quyền Duyệt = mục menu #/wf-inbox?type=scm-po (D-31). T15.1: PO ≥ ngưỡng GĐ → mục #/wf-inbox?type=scm-po-director;
        /// mục quyền phụ thuộc tổng tiền của PO nên engine (ApprovalRequestService.Decide) kiểm, không gắn cứng 1 màn hình ở filter (như ApprovalRequest/Decide).
        /// </summary>
        [Security.SkipPermission] // T15.1: quyền duyệt kiểm trong engine theo tổng tiền PO (scm-po / scm-po-director)
        [HttpPost("Approve")]
        public async Task<IActionResult> Approve([FromForm] PurchaseActionRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Decide(request, true));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Approve: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T15: từ chối PO (lý do bắt buộc) qua engine T12 – PO về Nháp.</summary>
        [Security.SkipPermission] // T15.1: quyền duyệt kiểm trong engine theo tổng tiền PO (scm-po / scm-po-director)
        [HttpPost("Reject")]
        public async Task<IActionResult> Reject([FromForm] PurchaseActionRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Decide(request, false));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Reject: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T15: Đã duyệt → Đã gửi NCC.</summary>
        [HttpPost("Send")]
        public async Task<IActionResult> Send([FromForm] PurchaseActionRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Send(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Send: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T15: hủy PO chưa có phiếu nhập (lý do bắt buộc).</summary>
        [HttpPost("Cancel")]
        public async Task<IActionResult> Cancel([FromForm] PurchaseCancelRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Cancel(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Cancel: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T15: Nhận một phần / Đã nhận đủ → Đóng.</summary>
        [HttpPost("Close")]
        public async Task<IActionResult> Close([FromForm] PurchaseActionRequest request)
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

        /// <summary>T15: in Đơn đặt hàng NCC (.xlsx) – quyền Xuất scm-po.</summary>
        [HttpGet("ExportOrder")]
        public async Task<IActionResult> ExportOrder(Guid id)
        {
            try
            {
                var result = await _service.ExportOrder(id);
                if (result.ResponseCode != Convert.ToInt32(HttpStatusCode.OK) || result.Data == null)
                {
                    return Ok(result);
                }
                return File(result.Data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Don_dat_hang_{id:N}.xlsx");
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportOrder: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
