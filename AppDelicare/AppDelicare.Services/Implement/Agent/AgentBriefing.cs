using System.Text;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement.Knowledge;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using AppDelicare.Services.Jobs;
using Microsoft.Extensions.Options;

namespace AppDelicare.Services.Implement.Agent
{
    public sealed class BriefingRunModel
    {
        public string Id { get; set; } = string.Empty;
        public int Recipients { get; set; }
        public int Sent { get; set; }
        public int Skipped { get; set; }
        public string? KnowledgePath { get; set; }
        /// <summary>Bản tin đầy đủ nhất (người nhận có nhiều khối nhất – ghi kho / thử).</summary>
        public List<string> Lines { get; set; } = [];
    }

    /// <summary>
    /// AI-25.4 (D-143): bản tin dựng bằng MẪU từ <c>ban-tin.yaml</c> – KHÔNG qua AI. Người nhận = người có quyền Xem mục <c>#/&lt;muc_quyen&gt;</c> (5 mục ẩn ai-ban-tin-*).
    /// Mỗi người: chạy lần lượt các câu nhanh bằng QUYỀN NGƯỜI ĐÓ (<see cref="IAgentQuickService.RunQuickFor"/> – câu ngoài quyền bỏ qua), ghép 1 tin
    /// (khối: tiêu đề · tóm tắt · tối đa <c>dong</c> dòng; khối rỗng → câu <c>rong</c> hoặc bỏ khi <c>an_khi_rong</c>) → OutboundMessage bot DH / VH (mẫu <c>AGENT_BRIEFING</c>,
    /// 1 tin / người / bản tin / ngày). Bản tin Ban GĐ (kênh DH) ghi 1 ghi chú / ngày vào thư mục 10 Ban GĐ (bản đầy đủ nhất trong số người nhận – chỉ đọc).
    /// Nhận xét AI cuối tin chỉ khi <c>Agent:BriefingAiComment = true</c> (mặc định false → 0 token); Hermes tắt / lỗi → vẫn gửi đủ phần mẫu.
    /// </summary>
    public interface IAgentBriefingService
    {
        Task<BriefingRunModel> RunAsync(string id, DateTime now, CancellationToken ct = default);

        /// <summary>Bản tin mẫu <paramref name="id"/> đang dùng (Agent:TemplateBriefings + có trong ban-tin.yaml) – job bản tin cũ cùng giờ thì bỏ qua.</summary>
        bool Active(string id);
    }

    public sealed class AgentBriefingService(IUnitOfWork unitOfWork, IAgentQuickService quick, INotificationService notifications, IKnowledgeService knowledge,
        IAgentChatService chat, ICurrentActor currentActor, IOptionsMonitor<AgentOptions> options) : IAgentBriefingService
    {
        public const string Template = NotificationCatalog.AgentBriefing;

        public bool Active(string id) => options.CurrentValue.TemplateBriefings && quick.Briefs().Any(b => b.Id == id);

        public async Task<BriefingRunModel> RunAsync(string id, DateTime now, CancellationToken ct = default)
        {
            var brief = quick.Briefs().FirstOrDefault(b => b.Id == id) ?? throw new InvalidOperationException("Không có bản tin " + id + " trong ban-tin.yaml");
            var db = unitOfWork.DbContext;
            var users = await RecipientResolver.UsersWithMenuOnlyAsync(db, "#/" + brief.MucQuyen, ct: ct);
            var m = new BriefingRunModel { Id = id, Recipients = users.Count };
            string bot = string.Equals(brief.Kenh, "DH", StringComparison.OrdinalIgnoreCase) ? MessagingCode.BotDieuHanh : MessagingCode.BotVanHanh;
            foreach (var user in users.OrderBy(u => u))
            {
                var lines = await ComposeAsync(brief, user);
                if (lines.Count == 0)
                {
                    m.Skipped++;
                    continue;
                }
                if (options.CurrentValue.BriefingAiComment && await CommentAsync(user, brief, lines) is { Length: > 0 } c)
                {
                    lines.Add("🤖 " + c);
                }
                if (lines.Count > m.Lines.Count)
                {
                    m.Lines = lines;
                }
                var sent = await BusinessNotifier.SafeEnqueueAsync(notifications, new NotificationRequest
                {
                    Template = Template, UserIds = [user], BotCode = bot,
                    Data = new { title = $"{brief.Ten} · {now:dd/MM}", lines },
                    // Bản tin Ban GĐ giữ 2 nút của bản tin bot cũ (MorningBrief / DailyReport).
                    Buttons = bot == MessagingCode.BotDieuHanh ? new List<NotificationButton> { new("📊 Tổng quan", "sc:run:dh_overview"), new("🚨 Việc đang mở", "sc:run:dh_open") } : null,
                    IdempotencyKey = $"BRIEF:{id}:{now:yyyyMMdd}:{user:N}", CorrelationId = $"brief:{id}:{now:yyyyMMdd}",
                });
                m.Sent += sent > 0 ? 1 : 0;
            }
            if (bot == MessagingCode.BotDieuHanh && m.Lines.Count > 0)
            {
                var w = await knowledge.WriteBriefingAsync(brief.Id, brief.Ten, now, m.Lines);
                m.KnowledgePath = w.Data?.Path;
            }
            return m;
        }

        /// <summary>Các dòng tin của 1 người (rỗng = mọi khối ngoài quyền / bị bỏ).</summary>
        private async Task<List<string>> ComposeAsync(QuickBrief brief, Guid user)
        {
            var lines = new List<string>();
            foreach (var k in brief.Khoi)
            {
                var r = await quick.RunQuickFor(user, k.Cau, brief.Kenh);
                if (r.ResponseCode != 200 || r.Data is not { } d)
                {
                    continue; // ngoài quyền người nhận / câu lỗi → bỏ khối
                }
                bool empty = d.Dong.Count == 0;
                if (empty && k.AnKhiRong)
                {
                    continue;
                }
                lines.Add("▪ " + d.TieuDe + (string.IsNullOrWhiteSpace(d.TomTat) ? string.Empty : ": " + d.TomTat));
                if (empty)
                {
                    if (!string.IsNullOrWhiteSpace(d.Rong) && string.IsNullOrWhiteSpace(d.TomTat))
                    {
                        lines.Add("   " + d.Rong);
                    }
                    continue;
                }
                foreach (var row in d.Dong.Take(Math.Max(0, k.Dong)))
                {
                    lines.Add("   • " + string.Join(" · ", row.Where(c => c.Length > 0 && c != "–")));
                }
                if (k.Dong > 0 && d.TongDong > Math.Min(k.Dong, d.Dong.Count))
                {
                    lines.Add($"   … còn {d.TongDong - Math.Min(k.Dong, d.Dong.Count)}");
                }
            }
            return lines;
        }

        private async Task<string?> CommentAsync(Guid user, QuickBrief brief, List<string> lines)
        {
            try
            {
                using var scope = currentActor.BeginScope(ActorInfo.ForUser(user));
                var r = await chat.Chat(new AgentChatRequest
                {
                    Agent = "VIVI",
                    Message = $"Nhận xét 2–3 dòng (điểm cần chú ý, việc nên làm) cho {brief.Ten.ToLowerInvariant()} sau – không nhắc lại số liệu:\n" + string.Join('\n', lines),
                });
                return r.ResponseCode == 200 ? r.Data?.Reply?.Trim() : null;
            }
            catch (Exception)
            {
                return null; // Hermes tắt / lỗi → gửi phần mẫu
            }
        }
    }

    /// <summary>AI-25.4: 1 job / bản tin (giờ = <c>gio</c> trong ban-tin.yaml, sửa giờ bằng <c>Jobs:&lt;Code&gt;:At</c>).</summary>
    public abstract class BriefingJobBase(IAgentBriefingService briefing, IAgentQuickService quick, string id, string code, string fallbackAt) : IScheduledJob
    {
        public string Code => code;
        public string Name => "Bản tin: " + (quick.Briefs().FirstOrDefault(b => b.Id == id)?.Ten ?? id);
        public string Description => $"Bản tin dựng bằng mẫu (không AI) '{id}' trong app-delicare-agents/ban-tin.yaml – gửi người có quyền mục bản tin qua bot, chạy câu nhanh bằng quyền từng người.";
        public string DefaultAt => quick.Briefs().FirstOrDefault(b => b.Id == id)?.Gio is { Length: 5 } g ? g : fallbackAt;

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            if (!briefing.Active(id))
            {
                return new JobOutcome(JobRunStatus.Skipped, 0, now.Date, "Bản tin mẫu đang tắt (Agent:TemplateBriefings) hoặc không có " + id + " trong ban-tin.yaml");
            }
            var r = await briefing.RunAsync(id, now, cancellationToken);
            return new JobOutcome(r.Recipients == 0 ? JobRunStatus.Skipped : JobRunStatus.Succeeded, r.Sent, now.Date,
                r.Recipients == 0 ? "Chưa ai có quyền mục bản tin" : $"Gửi {r.Sent} / {r.Recipients} người" + (r.Skipped > 0 ? $", {r.Skipped} người không có khối nào trong quyền" : string.Empty));
        }
    }

    public sealed class BriefingMorningBodJob(IAgentBriefingService b, IAgentQuickService q) : BriefingJobBase(b, q, "sang-bgd", "BriefingMorningBod", "07:00");

    public sealed class BriefingEveningBodJob(IAgentBriefingService b, IAgentQuickService q) : BriefingJobBase(b, q, "chieu-bgd", "BriefingEveningBod", "17:00");

    public sealed class BriefingWarehouseJob(IAgentBriefingService b, IAgentQuickService q) : BriefingJobBase(b, q, "sang-kho", "BriefingWarehouse", "05:30");

    public sealed class BriefingKitchenJob(IAgentBriefingService b, IAgentQuickService q) : BriefingJobBase(b, q, "sang-bep", "BriefingKitchen", "05:45");

    public sealed class BriefingOrderCloseJob(IAgentBriefingService b, IAgentQuickService q) : BriefingJobBase(b, q, "chot-don", "BriefingOrderClose", "19:00");
}
