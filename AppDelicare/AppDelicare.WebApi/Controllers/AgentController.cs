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
    public class AgentController(IAgentAdminService service, IAgentFlowService flows, AppDelicare.Services.Implement.Knowledge.IKnowledgeService knowledge, IAgentCostService cost, IAgentChatService chat) : BaseController
    {
        private readonly IAgentChatService _chat = chat;
        private readonly IAgentCostService _cost = cost;
        private readonly AppDelicare.Services.Implement.Knowledge.IKnowledgeService _knowledge = knowledge;
        private readonly IAgentAdminService _service = service;
        private readonly IAgentFlowService _flows = flows;

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

        /// <summary>AI-11 (D-133): luồng liên phòng của 1 ngày – dựng từ CorrelationId (sự kiện → agent → đề xuất → duyệt → kết quả → tin).</summary>
        [HttpGet("GetFlows")]
        [Security.Permission("ai-flow", PermissionAction.View, OrScreens = ["ai-agents"])]
        public Task<IActionResult> GetFlows([FromQuery] DateTime? date) => Run(nameof(GetFlows), () => _flows.GetFlows(date));

        /// <summary>AI-11: bật / tắt 1 việc theo lịch (ghi app-delicare-agents/schedules.json – Hermes đọc lại).</summary>
        /// <summary>AI-12 (D-133): việc agent theo dõi (thư mục 98 kho tri thức) – theo quyền thư mục kho của người xem.</summary>
        /// <summary>AI-13 (D-133): chi phí Trợ lý AI theo agent / ngày của 1 tháng (yyyy-MM, trống = tháng này) + ngân sách.</summary>
        /// <summary>AI-14 (D-133): chat với agent trên web – mọi người đã đăng nhập; công cụ agent chạy với quyền giao nhau agent ∩ người hỏi (vé ký HMAC).</summary>
        [HttpPost("Chat")]
        [Security.SkipPermission]
        public Task<IActionResult> Chat([FromBody] AgentChatRequest request) => Run(nameof(Chat), () => _chat.Chat(request ?? new AgentChatRequest()));

        [HttpGet("GetCost")]
        [Security.Permission("ai-agents", PermissionAction.View, OrScreens = ["ai-agent-log"])]
        public Task<IActionResult> GetCost([FromQuery] string? month) => Run(nameof(GetCost), () => _cost.GetCost(month));

        [HttpGet("GetTasks")]
        [Security.Permission("ai-tasks", PermissionAction.View)]
        public Task<IActionResult> GetTasks([FromQuery] bool includeDone = false) => Run(nameof(GetTasks), () => _knowledge.GetTasks(includeDone));

        /// <summary>AI-12: đánh dấu Xong / mở lại / giao lại 1 việc agent theo dõi.</summary>
        [HttpPost("SetTask")]
        [Security.Permission("ai-tasks", PermissionAction.Edit)]
        public Task<IActionResult> SetTask([FromBody] AppDelicare.Services.Implement.Knowledge.KnowledgeTaskSetRequest request) => Run(nameof(SetTask), () => _knowledge.SetTask(request ?? new AppDelicare.Services.Implement.Knowledge.KnowledgeTaskSetRequest()));

        [HttpPost("SaveSchedule")]
        [Security.Permission("ai-schedule", PermissionAction.Edit)]
        public Task<IActionResult> SaveSchedule([FromForm] string? id, [FromForm] bool enabled) => Run(nameof(SaveSchedule), () => _flows.SaveSchedule(id, enabled));

        [HttpGet("GetSchedules")]
        [Security.Permission("ai-schedule", PermissionAction.View, OrScreens = ["ai-agents"])]
        public Task<IActionResult> GetSchedules() => Run(nameof(GetSchedules), _service.GetSchedules);
    }
}
