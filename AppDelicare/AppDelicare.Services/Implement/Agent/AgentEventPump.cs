using System.Text.Json;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AppDelicare.Services.Implement.Agent
{
    public sealed class EventPumpRunModel
    {
        public bool Active { get; set; }
        public int Alerts { get; set; }
        public int Sent { get; set; }
        public int AiComments { get; set; }
        public string? Reason { get; set; }
    }

    /// <summary>
    /// AI-25.5 (D-143): lịch <c>vivi-events</c> (mỗi 5′) chạy ở AppDelicare.Job theo MẪU – không gọi Hermes. Mỗi lần: lấy cảnh báo đang mở (Common/GetAlerts – cùng nguồn sự kiện AI-4)
    /// → mỗi cảnh báo 1 tin / ngày (khóa <c>AGENT_ALERT:EVT:&lt;mã&gt;:&lt;ngày&gt;</c>) mẫu AGENT_ALERT tới người có quyền <b>Thêm</b> màn của cảnh báo (bot VH; màn Điều hành → bot DH).
    /// Sự cố ATTP, yêu cầu duyệt, phản ánh, job lỗi đã có tin lõi riêng → không gửi lại. Chỉ cảnh báo mức cao <b>và</b> <c>Agent:EventAiAnalysis = true</c> (mặc định false) mới gọi
    /// Vivi viết 2–3 dòng nhận xét (dưới danh nghĩa người nhận đầu – quyền giao nhau như chat). Chạy khi mục vivi-events trong schedules.json đang bật và <c>mode = template</c>.
    /// </summary>
    public interface IAgentEventPumpService
    {
        Task<EventPumpRunModel> RunOnceAsync(CancellationToken ct = default);
    }

    public sealed class AgentEventPumpService(IUnitOfWork unitOfWork, ICommonService common, INotificationService notifications, IAgentChatService chat, ICurrentActor currentActor,
        IClock clock, IConfiguration configuration, IHostEnvironment env, IOptionsMonitor<AgentOptions> options) : IAgentEventPumpService
    {
        public const string JobCode = "AgentEventPump";
        public const string KeyPrefix = "AGENT_ALERT:EVT:";
        public const string ScheduleId = "vivi-events";

        /// <summary>Mục vivi-events trong schedules.json: (có, bật, mode).</summary>
        public static (bool Found, bool Enabled, string Mode) Schedule(IConfiguration configuration, string contentRoot)
        {
            string? file = AgentAdminService.SchedulesPath(configuration, contentRoot);
            if (file == null)
            {
                return (false, false, "template");
            }
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                foreach (var e in doc.RootElement.GetProperty("items").EnumerateArray())
                {
                    if (e.GetProperty("id").GetString() == ScheduleId)
                    {
                        return (true, !e.TryGetProperty("enabled", out var en) || en.ValueKind != JsonValueKind.False, e.TryGetProperty("mode", out var m) && m.GetString() == "ai" ? "ai" : "template");
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IOException)
            {
                Serilog.Log.Warning(ex, "[AgentEventPump] schedules.json sai định dạng");
            }
            return (false, false, "template");
        }

        public async Task<EventPumpRunModel> RunOnceAsync(CancellationToken ct = default)
        {
            var (found, enabled, mode) = Schedule(configuration, env.ContentRootPath);
            if (!found || !enabled || mode != "template")
            {
                return new EventPumpRunModel { Reason = !found ? "Không có mục vivi-events trong schedules.json" : !enabled ? "Lịch vivi-events đang tắt" : "vivi-events mode = ai (Hermes chạy)" };
            }
            var m = new EventPumpRunModel { Active = true };
            var db = unitOfWork.DbContext;
            string day = clock.Today.ToString("yyyyMMdd");
            // Cảnh báo lọc theo quyền người xem → lấy dưới quyền agent VIVI (Xem như CEO – như khi Vivi tự gọi lay_su_kien); chưa có tài khoản agent → quyền người chạy job.
            Guid? vivi = await (from x in db.UserExternalIdentity.AsNoTracking()
                               join c in db.IntegrationChannel.AsNoTracking() on x.IntegrationChannelId equals c.Id
                               where !x.IsDeleted && x.ExternalUserId == "VIVI" && c.ProviderCode == "HERMES"
                               select x.UserId).FirstOrDefaultAsync(ct);
            List<AppDelicare.Model.Model.AlertModel> alerts;
            using (vivi is not { } v || v == Guid.Empty ? null : currentActor.BeginScope(ActorInfo.ForAgent(v, "VIVI")))
            {
                alerts = (await common.GetAlerts(null)).ListData ?? [];
            }
            foreach (var a in alerts.Where(a => !string.IsNullOrWhiteSpace(a.Route)))
            {
                m.Alerts++;
                string key = $"{KeyPrefix}{a.Code}:{day}";
                if (await db.OutboundMessage.AsNoTracking().AnyAsync(o => !o.IsDeleted && o.IdempotencyKey != null && o.IdempotencyKey.StartsWith(key), ct))
                {
                    continue;
                }
                string screen = a.Route.Split('?')[0];
                var lines = new List<string>();
                if (a.Severity == "HIGH" && options.CurrentValue.EventAiAnalysis && await CommentAsync(screen, a.Title, a.Detail) is { Length: > 0 } c)
                {
                    lines.Add("🤖 " + c);
                    m.AiComments++;
                }
                int sent = await BusinessNotifier.ToMenusAsync(notifications, db, NotificationCatalog.AgentAlert, [screen], new
                {
                    title = (a.Severity == "HIGH" ? "🔴 " : a.Severity == "MEDIUM" ? "🟠 " : "🟢 ") + a.Title, detail = a.Detail, lines,
                }, key, correlationId: "evt:" + a.Code, requireAdd: true, botCode: screen.StartsWith("#/dash-", StringComparison.Ordinal) ? MessagingCode.BotDieuHanh : MessagingCode.BotVanHanh);
                m.Sent += sent;
            }
            return m;
        }

        private async Task<string?> CommentAsync(string screen, string title, string? detail)
        {
            try
            {
                var users = await RecipientResolver.UsersWithMenuOnlyAsync(unitOfWork.DbContext, screen, requireAdd: true);
                if (users.Count == 0)
                {
                    return null;
                }
                using var scope = currentActor.BeginScope(ActorInfo.ForUser(users.OrderBy(u => u).First()));
                var r = await chat.Chat(new AgentChatRequest { Agent = "VIVI", Message = $"Nhận xét 2–3 dòng (nguyên nhân có thể, việc nên làm ngay) cho cảnh báo: {title}. {detail}" });
                return r.ResponseCode == 200 ? r.Data?.Reply?.Trim() : null;
            }
            catch (Exception)
            {
                return null; // Hermes tắt / lỗi → gửi phần mẫu
            }
        }
    }
}
