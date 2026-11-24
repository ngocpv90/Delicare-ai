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
    public class PurchaseRequestController(IPurchaseRequestService service) : BaseController
    {
        private readonly IPurchaseRequestService _service = service;

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] PurchaseRequestSearchRequest request)
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

        /// <summary>UI-6.18 A (D-99): tạo đề nghị mua kèm dòng (số / ngày lập / người đề nghị máy chủ điền, Nháp; Submit = gửi duyệt luôn). JSON. Quyền Thêm.</summary>
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] AppDelicare.Model.Request.Custom.PurchaseRequestCreateRequest request)
        {
            try
            {
                return null == request ? BadRequest(ConstantHelper.Message.ModelStateMessage) : Ok(await _service.CreateWithLines(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Create: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.18 A: sửa dòng đề nghị Nháp (thay toàn bộ dòng). JSON. Quyền Sửa.</summary>
        [HttpPost("UpdateLines")]
        public async Task<IActionResult> UpdateLines([FromBody] AppDelicare.Model.Request.Custom.PurchaseRequestUpdateLinesRequest request)
        {
            try
            {
                return null == request ? BadRequest(ConstantHelper.Message.ModelStateMessage) : Ok(await _service.UpdateLines(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " UpdateLines: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.18 C: gợi ý NCC (≤ 3 thẻ, NCC gợi ý, lý do) + tồn hiện tại cho các dòng đang nhập. JSON (POST đọc). Quyền Xem.</summary>
        [HttpPost("GetSupplierSuggestion")]
        public async Task<IActionResult> GetSupplierSuggestion([FromBody] AppDelicare.Model.Request.Custom.SupplierSuggestRequest request)
        {
            try
            {
                return null == request ? BadRequest(ConstantHelper.Message.ModelStateMessage) : Ok(await _service.GetSupplierSuggestion(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetSupplierSuggestion: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        [HttpPost("Insert")]
        public async Task<IActionResult> Insert([FromForm] PurchaseRequestRequest request)
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
        public async Task<IActionResult> Update([FromForm] PurchaseRequestRequest request)
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
                PurchaseRequestRequest request = new()
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

        /// <summary>UI-6 (D-80): trang chi tiết toàn màn – quyền Xem scm-pr (PermissionConvention: Get* = Xem).</summary>
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

        /// <summary>T15: chi tiết đề nghị mua – dòng + NCC gợi ý + tiền ước tính + PO đã lập.</summary>
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

        /// <summary>T15 (spec 7.2 convert-to-po): lập PO Nháp theo NCC gợi ý (mỗi NCC 1 PO), đề nghị → Đã lập PO. Quyền Sửa scm-pr.</summary>
        [HttpPost("ConvertToPurchaseOrder")]
        public async Task<IActionResult> ConvertToPurchaseOrder([FromForm] PurchaseRequestConvertRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.ConvertToPurchaseOrder(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ConvertToPurchaseOrder: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.3 (D-46): Nháp → Chờ duyệt (ApprovalRequest PURCHASE_REQUEST 2 cấp: scm-pr-l1 → scm-pr-l2). Quyền Sửa scm-pr.</summary>
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
        /// BE2.3 (D-46): duyệt **cấp đang chờ** qua engine – quyền Duyệt = mục #/wf-inbox?type=scm-pr-l1 (cấp 1) / scm-pr-l2 (cấp 2); mục phụ thuộc cấp nên
        /// engine (ApprovalRequestService.Decide) kiểm, không gắn cứng 1 màn hình ở filter (như PurchaseOrder/Approve).
        /// </summary>
        [Security.SkipPermission] // BE2.3: quyền duyệt kiểm trong engine theo cấp đang chờ (scm-pr-l1 / scm-pr-l2)
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

        /// <summary>BE2.3 (D-46): từ chối ở cấp đang chờ (lý do bắt buộc) – đề nghị về Nháp kèm lý do.</summary>
        [Security.SkipPermission] // BE2.3: quyền duyệt kiểm trong engine theo cấp đang chờ (scm-pr-l1 / scm-pr-l2)
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

        /// <summary>T15: hủy đề nghị Nháp / Đã duyệt (lý do bắt buộc).</summary>
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
    }
}
