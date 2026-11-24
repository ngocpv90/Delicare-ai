// Sinh bởi tools/AppDelicare.CodeGen (T05), nay là file của dev (T12: duyệt 1 cấp) – CodeGen không ghi đè.
#nullable enable
using Microsoft.AspNetCore.Mvc;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Services.Interface;
using AppDelicare.Model.Request.Custom;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    public class ApprovalRequestController(IApprovalRequestService service) : BaseController
    {
        private readonly IApprovalRequestService _service = service;

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] ApprovalRequestSearchRequest request)
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
        public async Task<IActionResult> Insert([FromForm] ApprovalRequestRequest request)
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
        public async Task<IActionResult> Update([FromForm] ApprovalRequestRequest request)
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
                ApprovalRequestRequest request = new() { Id = id };
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

        /// <summary>T12: yêu cầu chờ tôi duyệt (hộp duyệt). type = screen-id để lọc, vd. menu-recipe. Chỉ trả loại mà người đang đăng nhập có quyền duyệt (mục menu "Phê duyệt › …").</summary>
        [Security.SkipPermission]
        [HttpGet("GetMyPending")]
        public async Task<IActionResult> GetMyPending(string? type, bool explicitOnly = false)
        {
            try
            {
                // BOT-1.4: explicitOnly = chỉ bước có quyền Duyệt được cấp rõ (không tính quyền admin) – bot "Chờ tôi duyệt" không ngập việc của admin.
                return Ok(await _service.GetMyPending(type, explicitOnly));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMyPending: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T12: duyệt (approve = true) / từ chối (approve = false, bắt buộc note). Quyền duyệt kiểm theo loại nghiệp vụ; nội dung đã sửa sau khi gửi → không duyệt được.</summary>
        [Security.SkipPermission]
        [HttpPost("Decide")]
        public async Task<IActionResult> Decide([FromForm] ApprovalDecideRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Decide(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Decide: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.1 (spec 11.5 GET approvals/{id}): yêu cầu + bước + quyết định + mục + bản ghi nghiệp vụ (kèm dòng), canDecide / canCancel.</summary>
        [Security.SkipPermission] // như GetMyPending: service kiểm người gửi / người có quyền Duyệt mục của yêu cầu / admin
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

        /// <summary>BE2.1 (spec 11.5 cancel): người gửi hủy yêu cầu còn chờ duyệt (bản ghi nghiệp vụ về Nháp) – quyền Xem hộp duyệt, service kiểm đúng người gửi / admin.</summary>
        [Security.SkipPermission] // như GetMyPending / Decide: service kiểm đúng người gửi / admin
        [HttpPost("Cancel")]
        public async Task<IActionResult> Cancel([FromForm] ApprovalCancelRequest request)
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

        /// <summary>APR-1.3 (D-81 h): yêu cầu tôi gửi còn đang mở – cấp, người đang giữ, đã chờ, nhắc được không.</summary>
        [Security.SkipPermission]
        [HttpGet("GetMySent")]
        public async Task<IActionResult> GetMySent()
        {
            try
            {
                return Ok(await _service.GetMySent());
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMySent: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>APR-1.3: yêu cầu tôi đã duyệt / từ chối (mới nhất trước).</summary>
        [Security.SkipPermission]
        [HttpGet("GetMyDecided")]
        public async Task<IActionResult> GetMyDecided(int page = 1, int pageSize = 20)
        {
            try
            {
                return Ok(await _service.GetMyDecided(page, pageSize));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMyDecided: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>APR-1.3 (D-81 d): duyệt nhiều yêu cầu Bình thường – JSON body (danh sách id); máy chủ kiểm lại canBulk + quyền từng id, trả kết quả từng id. Không từ chối hàng loạt.</summary>
        [Security.SkipPermission]
        [HttpPost("DecideBulk")]
        public async Task<IActionResult> DecideBulk([FromBody] ApprovalBulkRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.DecideBulk(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " DecideBulk: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>APR-1.3 (D-81 h): người gửi nhắc người duyệt cấp hiện tại qua bot – tối đa 1 lần / BusinessRules:ApprovalRemindHours giờ / yêu cầu.</summary>
        [Security.SkipPermission]
        [HttpPost("Remind")]
        public async Task<IActionResult> Remind([FromForm] ApprovalRemindRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Remind(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Remind: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>APR-1.3 (D-81 k): thống kê theo loại (số yêu cầu, % duyệt nguyên trạng, thời gian chờ TB) – admin / người có quyền Xem #/wf-inbox.</summary>
        [Security.SkipPermission]
        [HttpGet("GetStats")]
        public async Task<IActionResult> GetStats(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                return Ok(await _service.GetStats(fromDate, toDate));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetStats: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
