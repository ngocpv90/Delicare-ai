using System.Text.Json;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AppDelicare.Services.Implement.Agent
{
    public sealed class AssistantBotModel
    {
        /// <summary>Có cấu hình bot Trợ lý (BotConfiguration TRO_LY).</summary>
        public bool Configured { get; set; }
        public bool Active { get; set; }
        public string? BotName { get; set; }
        public string? Username { get; set; }
        public string? Link { get; set; }
        /// <summary>QR của link t.me (SVG vẽ ở máy chủ – D-70).</summary>
        public string? QrSvg { get; set; }
        /// <summary>Token chỉ báo đã / chưa cấu hình – KHÔNG trả giá trị.</summary>
        public bool TokenConfigured { get; set; }
        public DateTime? LastAnswerAt { get; set; }
        public int AsksToday { get; set; }
        public int AskersToday { get; set; }
        public int DeniedNotLinked { get; set; }
        public int DeniedNoRight { get; set; }
        public int DeniedOff { get; set; }
        /// <summary>Số người đang có quyền mục "Dùng bot Trợ lý".</summary>
        public int AllowedUsers { get; set; }
        public bool CanEdit { get; set; }
    }

    public sealed class AssistantBotSaveRequest
    {
        public bool? Active { get; set; }
        public string? Username { get; set; }
    }

    /// <summary>AI-23.3: khối "Bot Trợ lý" trên trang tài khoản của tôi.</summary>
    public sealed class MyAssistantModel
    {
        public bool Allowed { get; set; }
        public bool Active { get; set; }
        public bool Linked { get; set; }
        public string? Username { get; set; }
        public string? Link { get; set; }
        public string? QrSvg { get; set; }
    }

    /// <summary>
    /// AI-23.2 (D-142): tab "Bot Trợ lý" của Trung tâm Agent – trạng thái bot Telegram Trợ lý (BotConfiguration TRO_LY + IntegrationChannel TELEGRAM_TRO_LY), link + QR,
    /// chỉ số hôm nay từ IntegrationLog kênh HERMES (câu hỏi có người hỏi Telegram; bị từ chối theo mã CHUA_LIEN_KET / KHONG_CO_QUYEN_TRO_LY / BOT_TRO_LY_TAT).
    /// Bật / tắt + sửa username: quyền Sửa Trung tâm Agent (cùng người bật / tắt agent). Tắt → ToolGateway từ chối ngay mọi câu hỏi qua Telegram.
    /// </summary>
    public interface IAgentAssistantBotService
    {
        Task<IResult<AssistantBotModel>> GetBot();
        Task<IResult<AssistantBotModel>> SaveBot(AssistantBotSaveRequest request);
        Task<IResult<MyAssistantModel>> GetMine();
    }

    public sealed class AgentAssistantBotService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IPermissionService permissions, IConfiguration configuration)
        : IAgentAssistantBotService
    {
        private const string Bot = AgentCallResult.AssistantBot;

        private async Task<(BotConfiguration? Bot, IntegrationChannel? Channel)> LoadAsync(bool track)
        {
            var db = unitOfWork.DbContext;
            var q = track ? db.BotConfiguration : db.BotConfiguration.AsNoTracking();
            var b = await q.FirstOrDefaultAsync(x => x.BotCode == Bot && !x.IsDeleted);
            var c = b == null ? null : await (track ? db.IntegrationChannel : db.IntegrationChannel.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == b.IntegrationChannelId);
            return (b, c);
        }

        private static bool IsActive(BotConfiguration? b, IntegrationChannel? c) => b != null && b.BotStatus == MessagingCode.StatusActive && (c == null || c.ChannelStatus == MessagingCode.StatusActive);

        private static string? LinkOf(string? username) => string.IsNullOrWhiteSpace(username) ? null : "https://t.me/" + username.Trim().TrimStart('@');

        public async Task<IResult<AssistantBotModel>> GetBot()
        {
            var actor = currentActor.RequireActor();
            var (b, c) = await LoadAsync(false);
            var m = new AssistantBotModel
            {
                Configured = b != null, Active = IsActive(b, c), BotName = b?.BotName, Username = b?.BotUsername, Link = LinkOf(b?.BotUsername),
                CanEdit = permissions.IsAllowed(await permissions.GetAsync(actor.UserId), "ai-agents", PermissionAction.Edit),
            };
            m.QrSvg = m.Link == null ? null : QrCodeRenderer.Svg(m.Link, 4);
            string? secret = c?.SecretReference ?? b?.SecretReference;
            m.TokenConfigured = !string.IsNullOrWhiteSpace(secret) && !string.IsNullOrWhiteSpace(configuration[secret.Replace("__", ":")] ?? Environment.GetEnvironmentVariable(secret))
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN_TRO_LY"));
            var db = unitOfWork.DbContext;
            Guid? hermes = await db.IntegrationChannel.AsNoTracking().Where(x => x.ProviderCode == "HERMES" && !x.IsDeleted).Select(x => (Guid?)x.Id).FirstOrDefaultAsync();
            if (hermes != null)
            {
                DateTime d0 = clock.Today;
                var rows = await db.IntegrationLog.AsNoTracking()
                    .Where(l => l.IntegrationChannelId == hermes && l.DirectionCode == "IN" && !l.IsDeleted && l.RequestAt >= d0 && l.ErrorMessage != null && l.ErrorMessage.Contains("\"onBehalfOf\":\"")
                                && !l.ErrorMessage.Contains("\"onBehalfOf\":\"web:") && !l.ErrorMessage.Contains("\"onBehalfOf\":\"zalo:"))
                    .Select(l => new { l.RequestAt, l.LogStatus, l.ErrorMessage, l.CorrelationId }).ToListAsync();
                var askers = new HashSet<string>();
                foreach (var r in rows)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(r.ErrorMessage!);
                        var e = doc.RootElement;
                        string? code = e.TryGetProperty("code", out var cd) && cd.ValueKind == JsonValueKind.String ? cd.GetString() : null;
                        if (e.TryGetProperty("onBehalfOf", out var ob) && ob.GetString() is { } who)
                        {
                            askers.Add(who);
                        }
                        m.DeniedNotLinked += code == AgentCallResult.NotLinked ? 1 : 0;
                        m.DeniedNoRight += code == AgentCallResult.NoAssistantRight ? 1 : 0;
                        m.DeniedOff += code == AgentCallResult.AssistantOff ? 1 : 0;
                    }
                    catch (JsonException)
                    {
                        // dòng hỏng – bỏ qua
                    }
                }
                // 1 câu hỏi = 1 lượt (CorrelationId) – Hermes có thể gọi nhiều công cụ cho 1 câu.
                m.AsksToday = rows.Select(r => r.CorrelationId ?? r.RequestAt.ToString("O")).Distinct().Count();
                m.AskersToday = askers.Count;
                m.LastAnswerAt = rows.Where(r => r.LogStatus == "Succeeded").Select(r => (DateTime?)r.RequestAt).Max();
            }
            m.AllowedUsers = (await AppDelicare.Services.Implement.Messaging.RecipientResolver.UsersWithMenuOnlyAsync(db, "#/" + AgentCallResult.AssistantScreen)).Count;
            return Result<AssistantBotModel>.Success(m, "OK");
        }

        public async Task<IResult<AssistantBotModel>> SaveBot(AssistantBotSaveRequest request)
        {
            var actor = currentActor.RequireActor();
            if (!permissions.IsAllowed(await permissions.GetAsync(actor.UserId), "ai-agents", PermissionAction.Edit))
            {
                return new Result<AssistantBotModel> { ResponseCode = 403, ResponseMessage = "Chỉ người có quyền Sửa Trung tâm Agent được bật / tắt bot Trợ lý" };
            }
            var (b, c) = await LoadAsync(true);
            if (b == null)
            {
                return Result<AssistantBotModel>.Fail("Chưa có cấu hình bot Trợ lý (BotConfiguration TRO_LY – seed Integration_Telegram_seed.sql)");
            }
            if (request.Username != null)
            {
                string u = request.Username.Trim().TrimStart('@');
                if (u.Length > 0 && (u.Length is < 5 or > 64 || !u.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_')))
                {
                    return Result<AssistantBotModel>.Fail("Tên bot Telegram chỉ gồm chữ không dấu, số, gạch dưới (5–64 ký tự), vd delicare_tro_ly_bot");
                }
                b.BotUsername = u.Length == 0 ? null : u;
            }
            if (request.Active is { } on)
            {
                b.BotStatus = on ? MessagingCode.StatusActive : "Inactive";
                if (c != null)
                {
                    c.ChannelStatus = on ? MessagingCode.StatusActive : "Inactive";
                }
            }
            await unitOfWork.DbContext.SaveChangesAsync();
            var r = await GetBot();
            r.ResponseMessage = request.Active switch { true => "Đã bật bot Trợ lý", false => "Đã tắt bot Trợ lý – câu hỏi qua Telegram bị từ chối ngay", _ => "Đã lưu" };
            return r;
        }

        public async Task<IResult<MyAssistantModel>> GetMine()
        {
            var actor = currentActor.RequireActor();
            var m = new MyAssistantModel { Allowed = permissions.IsAllowed(await permissions.GetAsync(actor.UserId), AgentCallResult.AssistantScreen, PermissionAction.View) };
            if (!m.Allowed)
            {
                return Result<MyAssistantModel>.Success(m, "OK");
            }
            var (b, c) = await LoadAsync(false);
            m.Active = IsActive(b, c);
            m.Username = b?.BotUsername;
            m.Link = LinkOf(b?.BotUsername);
            m.QrSvg = m.Link == null ? null : QrCodeRenderer.Svg(m.Link, 4);
            var db = unitOfWork.DbContext;
            m.Linked = await (from x in db.UserExternalIdentity.AsNoTracking()
                              join ch in db.IntegrationChannel.AsNoTracking() on x.IntegrationChannelId equals ch.Id
                              where x.UserId == actor.UserId && !x.IsDeleted && x.IdentityStatus == "Active" && ch.ProviderCode == "TELEGRAM"
                              select x.Id).AnyAsync();
            return Result<MyAssistantModel>.Success(m, "OK");
        }
    }
}
