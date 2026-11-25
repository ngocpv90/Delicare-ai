using Microsoft.AspNetCore.Mvc;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    public class OrganizationUnitController(IOrganizationUnitService service) : BaseController
    {
        private readonly IOrganizationUnitService _service = service;

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] OrganizationUnitSearchRequest request)
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
        public async Task<IActionResult> Insert([FromForm] OrganizationUnitRequest request)
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
        public async Task<IActionResult> Update([FromForm] OrganizationUnitRequest request)
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
                OrganizationUnitRequest request = new()
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

        /// <summary>T45R: cây đơn vị tổ chức kèm số nhân viên, số kho, tên quản lý.</summary>
        // ------------------------------------------------------------ UI-6.41 (D-122): Công ty & đơn vị

        /// <summary>Cây có công ty làm gốc + số nhân viên gồm con, cờ chưa có trưởng, tên loại tiếng Việt.</summary>
        [HttpGet("GetOrgTree")]
        [Security.Permission("adm-orgunit", PermissionAction.View, OrScreens = ["adm-company"])]
        public Task<IActionResult> GetOrgTree(Guid? companyId) => Run(nameof(GetOrgTree), () => _service.GetOrgTree(companyId));

        [HttpGet("GetSummary")]
        [Security.Permission("adm-orgunit", PermissionAction.View, OrScreens = ["adm-company"])]
        public Task<IActionResult> GetSummary() => Run(nameof(GetSummary), () => _service.GetOrgSummary());

        /// <summary>Chuyển sang đơn vị khác (kiểm vòng lặp, đổi công ty cả nhánh).</summary>
        [HttpPost("Move")]
        [Security.Permission("adm-orgunit", PermissionAction.Edit)]
        public Task<IActionResult> Move([FromBody] AppDelicare.Model.Model.OrgMoveRequest request) => Run(nameof(Move), () => _service.Move(request));

        /// <summary>Ngừng hoạt động / Hoạt động lại.</summary>
        [HttpPost("SetStatus")]
        [Security.Permission("adm-orgunit", PermissionAction.Edit)]
        public Task<IActionResult> SetStatus([FromBody] AppDelicare.Model.Model.OrgSetStatusRequest request) => Run(nameof(SetStatus), () => _service.SetStatus(request));

        /// <summary>Đặt trưởng đơn vị tại chỗ.</summary>
        [HttpPost("SetManager")]
        [Security.Permission("adm-orgunit", PermissionAction.Edit)]
        public Task<IActionResult> SetManager([FromBody] AppDelicare.Model.Model.OrgSetManagerRequest request) => Run(nameof(SetManager), () => _service.SetManager(request));

        private async Task<IActionResult> Run<T>(string action, Func<Task<T>> run)
        {
            try
            {
                return Ok(await run());
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "] ", action, ": ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        [HttpGet("GetTree")]
        public async Task<IActionResult> GetTree(Guid? companyId)
        {
            try
            {
                return Ok(await _service.GetTree(companyId));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetTree: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T45R: danh sách chi nhánh (UnitType = BRANCH) cho bộ chọn đơn vị trên header – mọi user đã đăng nhập.</summary>
        [Security.SkipPermission]
        [HttpGet("GetBranches")]
        public IActionResult GetBranches()
        {
            try
            {
                return Ok(_service.GetBranches());
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetBranches: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
