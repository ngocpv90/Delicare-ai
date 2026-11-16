using Microsoft.AspNetCore.Mvc;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    public class UserRoleController(IUserRoleService service) : BaseController
    {
        private readonly IUserRoleService _service = service;

        [HttpGet("GetByUserGroupId")]
        public async Task<IActionResult> GetByUserGroupId(Guid userGroupId)
        {
            try
            {
                return Ok(await _service.GetByUserGroupId(userGroupId.ToString(), true));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetByUserGroupId: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        [HttpPut("Update")]
        public async Task<IActionResult> Update([FromForm] UserRoleRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Update(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Update: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T45R: ma trận quyền của nhóm – mọi menu đang hoạt động (menu chưa có dòng: id = 00000000-…, gửi kèm userGroupId + objectMenuId khi Update để tạo mới).</summary>
        [HttpGet("GetMatrix")]
        public async Task<IActionResult> GetMatrix(Guid userGroupId)
        {
            try
            {
                return Ok(await _service.GetMatrix(userGroupId));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMatrix: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T45R: cấp / thu hồi truy cập toàn bộ màn hình (menu cấp 2) của nhóm.</summary>
        [HttpPost("SaveAccessAll")]
        public async Task<IActionResult> SaveAccessAll([FromForm] UserRoleAccessAllRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.SaveAccessAll(request.UserGroupId, request.AllowAccess));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SaveAccessAll: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T45R: sao chép quyền từ nhóm nguồn sang nhóm đích.</summary>
        [HttpPost("CopyFromUserGroup")]
        public async Task<IActionResult> CopyFromUserGroup([FromForm] UserRoleCopyRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.CopyFromUserGroup(request.SourceUserGroupId, request.TargetUserGroupId));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CopyFromUserGroup: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T45R: xem trước menu + quyền nhóm nhận được và thành viên của nhóm. Chỉ admin.</summary>
        [HttpGet("GetPreview")]
        public async Task<IActionResult> GetPreview(Guid userGroupId)
        {
            try
            {
                return Ok(await _service.GetPreview(userGroupId));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetPreview: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T45R: quyền hiệu lực của 1 người dùng (OR các nhóm; admin = mọi quyền).</summary>
        [HttpGet("GetByUserId")]
        public async Task<IActionResult> GetByUserId(Guid userId)
        {
            try
            {
                return Ok(await _service.GetByUserId(userId));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetByUserId: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
