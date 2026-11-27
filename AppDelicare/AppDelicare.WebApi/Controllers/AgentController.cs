using System.Net;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Implement.Agent;
using AppDelicare.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>
    /// AI-6 (D-133) – Trợ lý AI: Trung tâm agent (<c>ai-agents</c>), Nhật ký agent (<c>ai-agent-log</c>), Lịch chạy (<c>ai-schedule</c>).
    /// Bật / tắt agent = Sửa Trung tâm agent (CEO / COO / FIN-IT) – đổi BotConfiguration.BotStatus, ToolGateway kiểm mỗi lời gọi nên có hiệu lực ngay.
    /// </summary>
    public class AgentController(IAgentAdminService service) : BaseController
    {
        private readonly IAgentAdminService _service = service;

        private async Task<IActionResult> Run<T>(string name, Func<Task<T>> call)
        {
            try
            {
                return Ok(await call());
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "] ", name, ": ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        [HttpGet("GetAll")]
        [Security.Permission("ai-agents", PermissionAction.View)]
        public Task<IActionResult> GetAll() => Run(nameof(GetAll), _service.GetAll);

        [HttpGet("GetById")]
        [Security.Permission("ai-agents", PermissionAction.View)]
        public Task<IActionResult> GetById([FromQuery] string? code) => Run(nameof(GetById), () => _service.GetById(code));

        [HttpPost("SaveIsActive")]
        [Security.Permission("ai-agents", PermissionAction.Edit)]
        public Task<IActionResult> SaveIsActive([FromForm] string? code, [FromForm] bool active) => Run(nameof(SaveIsActive), () => _service.SaveIsActive(code, active));

        [HttpGet("GetActivity")]
        [Security.Permission("ai-agent-log", PermissionAction.View, OrScreens = ["ai-agents"])]
        public Task<IActionResult> GetActivity([FromQuery] AgentActivityRequest request) => Run(nameof(GetActivity), () => _service.GetActivity(request ?? new AgentActivityRequest()));

        [HttpGet("GetSchedules")]
        [Security.Permission("ai-schedule", PermissionAction.View, OrScreens = ["ai-agents"])]
        public Task<IActionResult> GetSchedules() => Run(nameof(GetSchedules), _service.GetSchedules);
    }
}
