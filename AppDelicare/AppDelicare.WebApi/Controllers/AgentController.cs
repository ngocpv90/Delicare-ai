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
    public class AgentController(IAgentAdminService service, IAgentFlowService flows, AppDelicare.Services.Implement.Knowledge.IKnowledgeService knowledge, IAgentCostService cost, IAgentChatService chat, IAgentCenterService center, AppDelicare.Services.Implement.Agent.IAgentQuickService quick, AppDelicare.Services.Implement.Agent.IAgentAssistantBotService assistant) : BaseController
    {
        private readonly AppDelicare.Services.Implement.Agent.IAgentAssistantBotService _assistant = assistant;
        private readonly IAgentCenterService _center = center;
        private readonly AppDelicare.Services.Implement.Agent.IAgentQuickService _quick = quick;
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

        /// <summary>AI-23.2 (D-142): tab Bot Trợ lý – trạng thái, link + QR, chỉ số hôm nay (Xem Trung tâm Agent).</summary>
        [HttpGet("GetAssistantBot")]
        [Security.Permission("ai-agents", PermissionAction.View)]
        public Task<IActionResult> GetAssistantBot() => Run(nameof(GetAssistantBot), _assistant.GetBot);

        /// <summary>AI-23.2: bật / tắt bot Trợ lý, sửa tên bot (Sửa Trung tâm Agent).</summary>
        [HttpPost("SaveAssistantBot")]
        [Security.Permission("ai-agents", PermissionAction.Edit)]
        public Task<IActionResult> SaveAssistantBot([FromBody] AppDelicare.Services.Implement.Agent.AssistantBotSaveRequest request) => Run(nameof(SaveAssistantBot), () => _assistant.SaveBot(request ?? new()));

        /// <summary>AI-23.3: khối Bot Trợ lý trên trang tài khoản – ai đăng nhập cũng gọi được (không có quyền → Allowed = false).</summary>
        [HttpGet("GetMyAssistant")]
        [Security.SkipPermission]
        public Task<IActionResult> GetMyAssistant() => Run(nameof(GetMyAssistant), _assistant.GetMine);

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

        /// <summary>AI-21 (D-138 g): chi phí AI chỉ CEO / COO / FIN-IT (mục quyền ẩn ai-cost) – người khác 403.</summary>
        [HttpGet("GetCost")]
        [Security.Permission("ai-cost", PermissionAction.View)]
        public Task<IActionResult> GetCost([FromQuery] string? month) => Run(nameof(GetCost), () => _cost.GetCost(month));

        [HttpGet("GetTasks")]
        [Security.Permission("ai-tasks", PermissionAction.View)]
        public Task<IActionResult> GetTasks([FromQuery] bool includeDone = false) => Run(nameof(GetTasks), () => _center.GetTasks(includeDone));

        /// <summary>AI-21 (D-138 d): "Đã xử lý" 1 việc – ghi chú vào sổ việc / ghi chú việc thư mục 98.</summary>
        [HttpPost("CloseTask")]
        [Security.Permission("ai-tasks", PermissionAction.Edit)]
        public Task<IActionResult> CloseTask([FromBody] AgentCloseTaskRequest request) => Run(nameof(CloseTask), () => _center.CloseTask(request ?? new AgentCloseTaskRequest()));

        /// <summary>AI-21: Tổng quan Trung tâm Agent (13 agent, lượt hôm nay, lịch, kho tri thức, việc mở, bước luồng cần chú ý, hoạt động gần đây).</summary>
        [HttpGet("GetOverview")]
        [Security.Permission("ai-agents", PermissionAction.View)]
        public Task<IActionResult> GetOverview() => Run(nameof(GetOverview), _center.GetOverview);

        /// <summary>AI-21 (D-138 f): danh sách agent cho tab Chat – ai vào được Trung tâm Agent (bất kỳ tab nào) đều xem.</summary>
        /// <summary>AI-25.1 (D-143): câu hỏi nhanh người dùng được hỏi (quyền Xem màn của công cụ) – agent (trống = mọi agent), kênh web / VH / DH.</summary>
        [HttpGet("GetQuick")]
        [Security.SkipPermission]
        public Task<IActionResult> GetQuick([FromQuery] string? agent, [FromQuery] string? kenh) => Run(nameof(GetQuick), () => _quick.GetQuick(agent, kenh));

        /// <summary>AI-25.1: chạy 1 câu nhanh – KHÔNG qua AI (0 token), bằng quyền người hỏi; kết quả đã che / cắt dòng + tóm tắt theo mẫu.</summary>
        [HttpPost("RunQuick")]
        [Security.SkipPermission]
        public Task<IActionResult> RunQuick([FromBody] AppDelicare.Services.Implement.Agent.QuickRunRequest request, [FromQuery] string? kenh) => Run(nameof(RunQuick), () => _quick.RunQuick(request ?? new AppDelicare.Services.Implement.Agent.QuickRunRequest(), kenh ?? "web"));

        [HttpGet("GetChatAgents")]
        [Security.Permission("ai-agents", PermissionAction.View, OrScreens = ["ai-tasks", "ai-flow", "ai-agent-log", "ai-schedule", "ai-cost"])]
        public Task<IActionResult> GetChatAgents() => Run(nameof(GetChatAgents), _center.GetChatAgents);

        /// <summary>AI-21 (D-138 e): 12 bước QT-SX-01 của 1 ngày (tính từ bảng sẵn có) + chuỗi xử lý theo CorrelationId.</summary>
        [HttpGet("GetFlowDay")]
        [Security.Permission("ai-flow", PermissionAction.View)]
        public Task<IActionResult> GetFlowDay([FromQuery] DateTime? date) => Run(nameof(GetFlowDay), () => _center.GetFlowDay(date));

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
