#nullable enable
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
    public class MenuChangeRequestController(IMenuChangeRequestService service) : BaseController
    {
        private readonly IMenuChangeRequestService _service = service;

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] MenuChangeRequestSearchRequest request)
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
        public async Task<IActionResult> Insert([FromForm] MenuChangeRequestRequest request)
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
        public async Task<IActionResult> Update([FromForm] MenuChangeRequestRequest request)
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
                MenuChangeRequestRequest request = new() { Id = id };
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

        /// <summary>UI-6.13 (D-91): xem trước đổi món ô thực đơn – số khách / đơn / suất ảnh hưởng + 4 điều kiện tự duyệt. Quyền Xem.</summary>
        [HttpGet("GetCellPreview")]
        public async Task<IActionResult> GetCellPreview([FromQuery] AppDelicare.Model.Model.MenuChangeCellRequest request)
        {
            try
            {
                return Ok(await _service.PreviewCell(request ?? new()));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetCellPreview: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.13: gửi đổi món cho mọi khách của ô (JSON) – tự duyệt khi cùng nhóm + có công thức khóa + FC đạt + trước giờ chốt, còn lại COO duyệt. Quyền Thêm.</summary>
        [HttpPost("CreateForCell")]
        public async Task<IActionResult> CreateForCell([FromBody] AppDelicare.Model.Model.MenuChangeCellRequest request)
        {
            try
            {
                if (null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.CreateForCell(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CreateForCell: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.13: tab "Đổi món" của Thực đơn tuần – mọi yêu cầu đổi món có ngày ăn trong tuần. Quyền Xem.</summary>
        [HttpGet("GetWeekChanges")]
        public async Task<IActionResult> GetWeekChanges([FromQuery] AppDelicare.Model.Model.MenuWeekRequest request)
        {
            try
            {
                return Ok(await _service.GetWeekChanges(request ?? new()));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetWeekChanges: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.2: chi tiết yêu cầu đổi món – tên món, đơn, yêu cầu duyệt, kế hoạch sản xuất của ngày (cần xử lý tay hay không).</summary>
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

        /// <summary>BE2.2 (spec 6.4, Catalog 104): tạo yêu cầu đổi món (số PH-) và gửi duyệt MENU_CHANGE.</summary>
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromForm] MenuChangeCreateRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Create(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Create: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.2: duyệt đổi món qua engine T12 – quyền Duyệt mục #/wf-inbox?type=menu-change-request kiểm trong engine.</summary>
        [Security.SkipPermission] // quyền duyệt kiểm trong engine (ApprovalRequestService.Decide)
        [HttpPost("Approve")]
        public async Task<IActionResult> Approve([FromForm] MenuChangeActionRequest request)
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

        /// <summary>BE2.2: từ chối đổi món (lý do bắt buộc) qua engine T12.</summary>
        [Security.SkipPermission] // quyền duyệt kiểm trong engine (ApprovalRequestService.Decide)
        [HttpPost("Reject")]
        public async Task<IActionResult> Reject([FromForm] MenuChangeActionRequest request)
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
    }
}
