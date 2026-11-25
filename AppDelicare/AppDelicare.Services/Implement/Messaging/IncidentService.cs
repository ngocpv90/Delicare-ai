using System.Text.Json;
using System.Text.Json.Nodes;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace AppDelicare.Services.Implement.Messaging
{
    /// <summary>Một bậc leo thang (BOT_TELEGRAM §5.2): sau <see cref="AfterMinutes"/> phút kể từ lúc báo mà chưa ai nhận → gửi nhóm quyền <see cref="Groups"/>.</summary>
    public sealed class EscalationTier
    {
        public int AfterMinutes { get; set; }
        public List<string> Groups { get; set; } = [];
    }

    /// <summary>
    /// BOT-2.2 (BOT_TELEGRAM §5.2, ROADMAP BOT-2.2): sự việc liên phòng = các tin KHẨN cùng CorrelationId. [🙋 Nhận xử lý] (người bấm trước được ghi, người sau thấy tên),
    /// [💬 Ghi chú] (nối vào thẻ mọi người), [✅ Đã xử lý]. Trạng thái lưu trong <c>OutboundMessage.PayloadJson.incident</c> của mọi tin cùng sự việc (GAP-INT-04 – chưa có chỗ
    /// lưu riêng, không thêm bảng). Leo thang: chưa ai nhận sau N′ → INCIDENT_UNCLAIMED tới bậc kế (cấu hình <c>Messaging:Escalation:&lt;mã mẫu&gt;</c>, PROPOSED).
    /// Gọi từ messaging (khóa client) → ghi dưới tài khoản hệ thống.
    /// </summary>
    public class IncidentService(IUnitOfWork unitOfWork, IClock clock, IConfiguration configuration, ICurrentActor currentActor, INotificationService notifications)
        : IIncidentService
    {
        /// <summary>Mặc định (PROPOSED – chưa có nhóm "bếp trưởng / trưởng QA" riêng): 10′ → COO, 30′ → CEO-PMO.</summary>
        public static readonly Dictionary<string, List<EscalationTier>> DefaultTiers = new(StringComparer.OrdinalIgnoreCase)
        {
            [NotificationCatalog.FoodSafetyCritical] = [new() { AfterMinutes = 10, Groups = ["COO"] }, new() { AfterMinutes = 30, Groups = ["CEO-PMO"] }],
            [NotificationCatalog.CoreTempLow] = [new() { AfterMinutes = 10, Groups = ["COO"] }, new() { AfterMinutes = 30, Groups = ["CEO-PMO"] }],
            [NotificationCatalog.IotOutOfRange] = [new() { AfterMinutes = 30, Groups = ["COO"] }],
            [NotificationCatalog.QcIncomingFail] = [new() { AfterMinutes = 10, Groups = ["COO"] }, new() { AfterMinutes = 30, Groups = ["CEO-PMO"] }],
            [NotificationCatalog.DeliveryTempLow] = [new() { AfterMinutes = 10, Groups = ["COO"] }, new() { AfterMinutes = 30, Groups = ["CEO-PMO"] }],
            [NotificationCatalog.ShortageReported] = [new() { AfterMinutes = 15, Groups = ["COO"] }, new() { AfterMinutes = 45, Groups = ["CEO"] }],
            // BOT-2.3: xe chưa rời / điểm trễ (BOT_TELEGRAM §5.2 – bậc 3 = COO; chưa có nhóm "trưởng điều phối").
            [NotificationCatalog.TripNotDeparted] = [new() { AfterMinutes = 10, Groups = ["COO"] }],
            [NotificationCatalog.StopLate] = [new() { AfterMinutes = 10, Groups = ["COO"] }],
        };

        private Dictionary<string, List<EscalationTier>> Tiers
        {
            get
            {
                var section = configuration.GetSection("Messaging:Escalation");
                var configured = section.Get<Dictionary<string, List<EscalationTier>>>();
                return configured is { Count: > 0 } ? new(configured, StringComparer.OrdinalIgnoreCase) : DefaultTiers;
            }
        }

        private bool TrySystemUser(out Guid systemUser)
            => Guid.TryParse(configuration[$"{SystemUsersOptions.Section}:{nameof(SystemUsersOptions.JobUserId)}"], out systemUser) && systemUser != Guid.Empty;

        public async Task<IResult<IncidentResultModel>> Act(IncidentActionRequest request)
        {
            if (!TrySystemUser(out Guid systemUser))
            {
                return Result<IncidentResultModel>.Fail(ConstantHelper.Message.JobUserMissing);
            }
            var db = unitOfWork.DbContext;
            var bots = await RecipientResolver.ActiveBotsAsync(db);
            if (!bots.TryGetValue(request.BotCode ?? string.Empty, out Guid channel))
            {
                return Result<IncidentResultModel>.Fail(string.Format(ConstantHelper.Message.BotUnknown, request.BotCode));
            }
            var tapped = await db.OutboundMessage.AsNoTracking().FirstOrDefaultAsync(m => !m.IsDeleted && m.IntegrationChannelId == channel
                && m.ProviderMessageId == request.ProviderMessageId && (m.ExternalConversationId == request.ChatId || m.RecipientExternalId == request.ChatId));
            if (tapped == null)
            {
                return Result<IncidentResultModel>.Fail(ConstantHelper.Message.IncidentNotFound);
            }
            string who = await NameOfAsync(channel, request.ExternalUserId) ?? "Người dùng bot";
            string correlation = tapped.CorrelationId ?? "MSG:" + tapped.Id.ToString("N");
            using var scope = currentActor.BeginScope(ActorInfo.ForSystem(systemUser));
            using var tran = await unitOfWork.OpenTransaction();
            try
            {
                await DbLocks.AcquireAsync(db, "Incident:" + correlation);
                var messages = tapped.CorrelationId == null
                    ? await db.OutboundMessage.Where(m => m.Id == tapped.Id).ToListAsync()
                    : await db.OutboundMessage.Where(m => !m.IsDeleted && m.CorrelationId == correlation).ToListAsync();
                var state = messages.Select(m => IncidentOf(m.PayloadJson)).FirstOrDefault(s => s != null) ?? new JsonObject();
                DateTime now = clock.Now;
                string? note = null;
                bool changed = true;
                switch (request.Action)
                {
                    case "claim":
                        if (state["claimedBy"] is { } by)
                        {
                            changed = false;
                            note = string.Format(ConstantHelper.Message.IncidentAlreadyClaimed, by.GetValue<string>(), state["claimedAt"]?.GetValue<string>());
                        }
                        else
                        {
                            state["claimedBy"] = who;
                            state["claimedAt"] = now.ToString("HH:mm");
                            note = ConstantHelper.Message.IncidentClaimed;
                        }
                        break;
                    case "done":
                        if (state["resolvedBy"] != null)
                        {
                            changed = false;
                            note = ConstantHelper.Message.IncidentAlreadyResolved;
                        }
                        else
                        {
                            state["claimedBy"] ??= who;
                            state["claimedAt"] ??= now.ToString("HH:mm");
                            state["resolvedBy"] = who;
                            state["resolvedAt"] = now.ToString("HH:mm");
                            note = ConstantHelper.Message.IncidentResolved;
                        }
                        break;
                    case "note" when !string.IsNullOrWhiteSpace(request.Text):
                        var notes = state["notes"] as JsonArray ?? [];
                        notes.Add(new JsonObject { ["by"] = who, ["at"] = now.ToString("HH:mm"), ["text"] = request.Text.Trim()[..Math.Min(request.Text.Trim().Length, 300)] });
                        while (notes.Count > 10)
                        {
                            notes.RemoveAt(0);
                        }
                        state["notes"] = notes;
                        note = ConstantHelper.Message.IncidentNoted;
                        break;
                    default:
                        unitOfWork.RollbackTransaction();
                        return Result<IncidentResultModel>.Fail(ConstantHelper.Message.ModelStateMessage);
                }
                if (changed)
                {
                    foreach (var m in messages)
                    {
                        m.PayloadJson = WithIncident(m.PayloadJson, state);
                        unitOfWork.OutboundMessageRepository.Update(m);
                    }
                    await unitOfWork.OutboundMessageRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                }
                unitOfWork.CommitTransaction();
                var botOf = bots.ToDictionary(kv => kv.Value, kv => kv.Key);
                return Result<IncidentResultModel>.Success(new IncidentResultModel
                {
                    Changed = changed,
                    Note = note,
                    ListEdit = changed
                        ? [.. messages.Where(m => m.MessageStatus == MessagingCode.Outbound.Sent && m.ProviderMessageId != null && botOf.ContainsKey(m.IntegrationChannelId))
                            .Select(m => new BotMessageEditModel { BotCode = botOf[m.IntegrationChannelId], ChatId = m.ExternalConversationId ?? m.RecipientExternalId, ProviderMessageId = m.ProviderMessageId, PayloadJson = m.PayloadJson ?? "{}" })]
                        : [],
                }, note ?? ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Act: ", ex.ToString()));
                unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>Leo thang (messaging gọi mỗi phút): sự việc trong 24 giờ, chưa ai nhận, đã qua mốc bậc → INCIDENT_UNCLAIMED tới nhóm của bậc (bot DH, chống trùng theo bậc).</summary>
        public async Task<IResult<int>> RunEscalation()
        {
            if (!TrySystemUser(out Guid systemUser))
            {
                return Result<int>.Success(0, ConstantHelper.Message.JobUserMissing);
            }
            var tiers = Tiers;
            var templates = tiers.Keys.ToList();
            DateTime now = clock.Now, since = now.AddHours(-24);
            var db = unitOfWork.DbContext;
            var rows = await db.OutboundMessage.AsNoTracking()
                .Where(m => !m.IsDeleted && m.CorrelationId != null && m.QueuedAt >= since
                            && (templates.Contains(m.MessageType!) || m.MessageType == NotificationCatalog.IncidentUnclaimed))
                .Select(m => new { m.CorrelationId, m.MessageType, m.QueuedAt, m.PayloadJson }).ToListAsync();
            int sent = 0;
            using var scope = currentActor.BeginScope(ActorInfo.ForSystem(systemUser));
            foreach (var g in rows.GroupBy(r => r.CorrelationId!))
            {
                var origin = g.Where(r => templates.Contains(r.MessageType!, StringComparer.OrdinalIgnoreCase)).OrderBy(r => r.QueuedAt).FirstOrDefault();
                if (origin == null || g.Any(r => IncidentOf(r.PayloadJson) is { } s && s["claimedBy"] != null))
                {
                    continue;
                }
                var list = tiers[origin.MessageType!];
                for (int i = 0; i < list.Count; i++)
                {
                    if (now < origin.QueuedAt.AddMinutes(list[i].AfterMinutes))
                    {
                        break;
                    }
                    var users = await (from uc in db.UserClaim.AsNoTracking()
                                       join ug in db.UserGroup.AsNoTracking() on uc.UserGroupId equals ug.Id
                                       join u in db.User.AsNoTracking() on uc.UserId equals u.Id
                                       where !uc.IsDeleted && !ug.IsDeleted && (list[i].Groups.Contains(ug.Code!) || (ug.Code == null && list[i].Groups.Contains(ug.Name!))) && !u.IsDeleted && u.IsActive
                                       select u.Id).Distinct().ToListAsync();
                    var original = JsonNode.Parse(origin.PayloadJson ?? "{}")?["data"];
                    sent += await BusinessNotifier.SafeEnqueueAsync(notifications, new NotificationRequest
                    {
                        Template = NotificationCatalog.IncidentUnclaimed, UserIds = users, BotCode = MessagingCode.BotDieuHanh,
                        Data = new
                        {
                            title = original?["title"]?.ToString(),
                            detail = $"Chưa ai nhận xử lý sau {list[i].AfterMinutes}′ (báo lúc {origin.QueuedAt:HH:mm}) – bậc {i + 2}",
                            lines = (original?["lines"] as JsonArray)?.Select(x => x?.ToString()).ToList(),
                        },
                        Buttons = BusinessNotifier.IncidentButtons,
                        IdempotencyKey = $"ESC:{g.Key}:{i + 2}", CorrelationId = g.Key, Escalation = true,
                    });
                }
            }
            return Result<int>.Success(sent, ConstantHelper.Message.GetAllSuccessfully);
        }

        private async Task<string?> NameOfAsync(Guid channel, string? externalUserId)
        {
            var db = unitOfWork.DbContext;
            var identity = await db.UserExternalIdentity.AsNoTracking()
                .FirstOrDefaultAsync(i => !i.IsDeleted && i.IntegrationChannelId == channel && i.ExternalUserId == externalUserId);
            if (identity?.UserId is { } uid)
            {
                var name = await (from u in db.User.AsNoTracking()
                                  join p in db.Person.AsNoTracking() on u.PersonId equals p.Id into pp
                                  from p in pp.DefaultIfEmpty()
                                  where u.Id == uid
                                  select p != null && p.FullName != null ? p.FullName : u.UserName).FirstOrDefaultAsync();
                if (name != null)
                {
                    return name;
                }
            }
            if (identity?.EmployeeId is { } eid)
            {
                return await db.Employee.AsNoTracking().Where(e => e.Id == eid).Select(e => e.FullName ?? e.Code).FirstOrDefaultAsync();
            }
            return identity?.DisplayName;
        }

        private static JsonObject? IncidentOf(string? payload)
        {
            try
            {
                return JsonNode.Parse(string.IsNullOrWhiteSpace(payload) ? "{}" : payload)?["incident"] as JsonObject;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string WithIncident(string? payload, JsonObject state)
        {
            JsonObject root;
            try
            {
                root = JsonNode.Parse(string.IsNullOrWhiteSpace(payload) ? "{}" : payload) as JsonObject ?? [];
            }
            catch (JsonException)
            {
                root = [];
            }
            root["incident"] = state.DeepClone();
            return root.ToJsonString(NotificationService.Json);
        }
    }
}
