using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Implement.Agent
{
    /// <summary>
    /// AI-9 (D-133): công cụ ghi 2 pha <c>propose_*</c> sinh từ <see cref="AgentCommandRegistry"/>. Gọi công cụ = kiểm tra + tạo đề xuất (ApprovalRequest AGENT_WRITE),
    /// KHÔNG ghi bảng nghiệp vụ. Người hỏi (nếu có) phải tự có quyền thao tác đó trên web (quyền giao nhau); <c>Agent:ReadOnly</c> chặn ở ToolGateway.
    /// </summary>
    public static partial class AgentToolRegistry
    {
        private static IEnumerable<AgentToolDef> CommandTools() => AgentCommandRegistry.All.Select(c => new AgentToolDef
        {
            Name = c.Code, ScreenId = c.ToolScreen ?? c.Screen, Agents = c.Agents, InputSchemaJson = c.InputSchemaJson,
            Description = c.Description + " Trả số đề xuất + hạn xác nhận; nói rõ với người hỏi là CHƯA ghi, chờ xác nhận.",
            RunAsync = async (ctx, a) =>
            {
                if (ctx.AskingUserId is { } asking)
                {
                    var perm = Svc<IPermissionService>(ctx);
                    if (!perm.IsAllowed(await perm.GetAsync(asking), c.Screen, c.Action))
                    {
                        throw new AgentToolException($"Người hỏi không có quyền {c.Action} trên màn {c.Screen} – không đề xuất thay được");
                    }
                }
                return await Svc<IAgentCommandService>(ctx).ProposeAsync(c, ctx, a);
            },
        });
    }
}
