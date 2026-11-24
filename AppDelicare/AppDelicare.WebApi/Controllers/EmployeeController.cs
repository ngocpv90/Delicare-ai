using Microsoft.AspNetCore.Mvc;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    public class EmployeeController(IEmployeeService service, IAccountLifecycleService accounts) : BaseController
    {
        private readonly IEmployeeService _service = service;

        // ------------------------------------------------------------ HR-ACC-1 (D-82): tài khoản ↔ con người ↔ nhân viên

        /// <summary>HR-ACC-1.6 (D-82 f): NS đề nghị cấp tài khoản web → yêu cầu duyệt "Cấp tài khoản" cho FIN-IT (nhóm gợi ý theo chức danh, đổi được khi duyệt).</summary>
        [HttpPost("RequestAccount")]
        [Security.Permission("adm-employee", PermissionAction.Edit)]
        public Task<IActionResult> RequestAccount([FromBody] AppDelicare.Model.Model.EmployeeAccountRequest request) => Run(nameof(RequestAccount), () => accounts.RequestCreate(request));

        /// <summary>HR-ACC-1.6 (D-82 h): nhân viên quay lại làm việc mà tài khoản đang khóa → yêu cầu "Mở lại tài khoản" (tự tạo khi đổi trạng thái; nút trên Nhân viên 360).</summary>
        [HttpPost("RequestReopen")]
        [Security.Permission("adm-employee", PermissionAction.Edit)]
        public Task<IActionResult> RequestReopen([FromBody] AppDelicare.Model.Model.EmployeeAccountRequest request) => Run(nameof(RequestReopen), () => accounts.RequestReopen(request));

        /// <summary>HR-ACC-1.8 (D-82 j): khối Tài khoản trên Nhân viên 360 (trạng thái, nhóm, yêu cầu đang chờ, nút theo quyền).</summary>
        [HttpGet("GetAccount")]
        public Task<IActionResult> GetAccount(Guid id) => Run(nameof(GetAccount), () => accounts.GetEmployeeAccount(id));

        /// <summary>HR-ACC-1.8 (D-82 d, j): tab Đối chiếu – nhân viên chưa gắn con người + ứng viên. group = SURE / MULTI / NONE (trống = tất cả).</summary>
        [HttpGet("GetLinkReview")]
        public Task<IActionResult> GetLinkReview(string? group) => Run(nameof(GetLinkReview), () => accounts.GetLinkReview(group));

        /// <summary>HR-ACC-1.8: gắn nhân viên với 1 người (PersonId) hoặc với người của 1 tài khoản (UserId); Unlink = gỡ. Quyền Sửa adm-employee + Sửa adm-user.</summary>
        [HttpPost("LinkPerson")]
        [Security.Permission("adm-employee", PermissionAction.Edit, AndScreens = ["adm-user"])]
        public Task<IActionResult> LinkPerson([FromBody] AppDelicare.Model.Model.EmployeeLinkPersonRequest request) => Run(nameof(LinkPerson), () => accounts.LinkPerson(request));

        private async Task<IActionResult> Run<T>(string action, Func<Task<T>> run)
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

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] EmployeeSearchRequest request)
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
        public async Task<IActionResult> Insert([FromForm] EmployeeRequest request)
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
        public async Task<IActionResult> Update([FromForm] EmployeeRequest request)
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
                EmployeeRequest request = new()
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

        /// <summary>UI-4 (D-68 g1): hồ sơ nhân viên – tài khoản, bot, chỉ số tháng, nghỉ phép, ca 7 ngày tới, giờ công 30 ngày, Cần chú ý. Quyền xem adm-employee.</summary>
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
    }
}
