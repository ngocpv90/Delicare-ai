using Microsoft.AspNetCore.Mvc;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    public class UserController(IUserService service, IAccountLifecycleService accounts) : BaseController
    {
        // ------------------------------------------------------------ HR-ACC-1 (D-82)

        /// <summary>HR-ACC-1.8 (D-82 j): màn Tài khoản – cột Nhân viên, nhãn loại (Nhân viên / Hệ thống / Khách hàng), cảnh báo "đã nghỉ nhưng còn mở", lọc "Chưa gắn".</summary>
        /// <summary>HR-ACC-1.8 (D-82 j): "Việc của tôi" – tài khoản đang đăng nhập đã gắn nhân viên chưa (mọi tài khoản đăng nhập, không cần quyền màn).</summary>
        [HttpGet("GetMyEmployee")]
        [Security.SkipPermission]
        public Task<IActionResult> GetMyEmployee() => RunAccount(nameof(GetMyEmployee), () => accounts.GetMyEmployee());

        [HttpGet("GetAccountLinks")]
        public Task<IActionResult> GetAccountLinks() => RunAccount(nameof(GetAccountLinks), () => accounts.GetAccountLinks());

        /// <summary>HR-ACC-1.6 (D-82 f): IT (Thêm adm-user) tạo thẳng tài khoản cho nhân viên – không qua duyệt; nhóm trống = gợi ý theo chức danh.</summary>
        [HttpPost("CreateForEmployee")]
        [Security.Permission("adm-user", PermissionAction.Add)]
        public Task<IActionResult> CreateForEmployee([FromBody] AppDelicare.Model.Model.UserCreateForEmployeeRequest request) => RunAccount(nameof(CreateForEmployee), () => accounts.CreateForEmployee(request));

        /// <summary>HR-ACC-1.6: mật khẩu tạm của tài khoản vừa duyệt cấp khi nhân viên chưa liên kết bot – xem 1 lần (30′). Người duyệt tài khoản hoặc Sửa adm-user.</summary>
        [HttpGet("TakeCreatedAccount")]
        [Security.Permission("adm-user", PermissionAction.Approve, OrActions = [PermissionAction.Edit])]
        public Task<IActionResult> TakeCreatedAccount(Guid employeeId) => RunAccount(nameof(TakeCreatedAccount), () => accounts.TakeCreated(employeeId));

        private async Task<IActionResult> RunAccount<T>(string action, Func<Task<T>> run)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await run());
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "] ", action, ": ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        private readonly IUserService _service = service;

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] UserSearchRequest request)
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

        // ------------------------------------------------------------ UI-6.42 (D-123): Tài khoản

        /// <summary>Danh sách tài khoản: người dùng, nhân viên, nhóm, truy cập gần nhất, bot, trạng thái + 4 chỉ số.</summary>
        [HttpGet("GetList")]
        [Security.Permission("adm-user", PermissionAction.View)]
        public Task<IActionResult> GetList([FromQuery] AppDelicare.Model.Model.AccountListRequest request) => RunAccount(nameof(GetList), () => _service.GetList(request));

        /// <summary>Chọn nhiều → thêm / bỏ nhóm quyền (không để tài khoản hết nhóm).</summary>
        [HttpPost("SetGroupsMany")]
        [Security.Permission("adm-user", PermissionAction.Edit)]
        public Task<IActionResult> SetGroupsMany([FromBody] AppDelicare.Model.Model.AccountIdsRequest request) => RunAccount(nameof(SetGroupsMany), () => _service.SetGroupsMany(request));

        /// <summary>Chọn nhiều → khóa (bỏ qua chính mình / quản trị cuối cùng).</summary>
        [HttpPost("LockMany")]
        [Security.Permission("adm-user", PermissionAction.Edit)]
        public Task<IActionResult> LockMany([FromBody] AppDelicare.Model.Model.AccountIdsRequest request) => RunAccount(nameof(LockMany), () => _service.LockMany(request));

        /// <summary>Tab "Chờ IT xử lý": yêu cầu tài khoản (D-82) đang chờ.</summary>
        [HttpGet("GetPendingRequests")]
        [Security.Permission("adm-user", PermissionAction.View)]
        public Task<IActionResult> GetPendingRequests() => RunAccount(nameof(GetPendingRequests), () => _service.GetPendingRequests());

        [HttpPost("Insert")]
        public async Task<IActionResult> Insert([FromForm] UserRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.InsertAccount(request)); // T45R.8: Result<PersonUserCreatedViewModel> (mật khẩu tạm trả một lần)
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Insert: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        [HttpPut("Update")]
        public async Task<IActionResult> Update([FromForm] UserRequest request)
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
                UserRequest request = new()
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

        [HttpGet("ResetPassword")]
        public async Task<IActionResult> ResetPassword(Guid id)
        {
            try
            {
                return Ok(await _service.ResetPassword(id));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ResetPassword: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        [HttpGet("SaveIsActive")]
        public async Task<IActionResult> SaveIsActive(Guid id)
        {
            try
            {
                return Ok(await _service.SaveIsActive(id));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SaveIsActive: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        [HttpGet("SaveIsAdmin")]
        public async Task<IActionResult> SaveIsAdmin(Guid id)
        {
            try
            {
                return Ok(await _service.SaveIsAdmin(id));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SaveIsAdmin: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        [HttpPut("ChangePassword")]
        public async Task<IActionResult> ChangePassword([FromForm] ChangePasswordRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.ChangePassword(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ChangePassword: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T45R: tài khoản đang đăng nhập + menu theo quyền hiệu lực (gọi sau đăng nhập / khi tải lại trang). Chỉ cần đăng nhập.</summary>
        [HttpGet("GetCurrent")]
        public async Task<IActionResult> GetCurrent()
        {
            try
            {
                return Ok(await _service.GetCurrent());
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetCurrent: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
