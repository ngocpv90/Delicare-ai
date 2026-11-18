using System.Text.Json;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement.Messaging
{
    /// <summary>
    /// BOT-1.1 (D-62): outbox. Service nghiệp vụ gọi <see cref="EnqueueAsync"/> TRONG transaction đang mở của mình → tin chỉ tồn tại khi nghiệp vụ commit
    /// (rollback → không có OutboundMessage). Lưu mã mẫu + dữ liệu (không câu chữ): <c>PayloadJson = { template, data, buttons, scope, level, notBefore? }</c>.
    /// Mỗi người nhận đã liên kết 1 dòng (bot theo phạm vi mẫu – <see cref="NotificationCatalog.BotFor"/>), chống trùng theo <c>IdempotencyKey:IdentityId</c>.
    /// Người chưa liên kết bot → bỏ qua (ghi log), không làm hỏng nghiệp vụ.
    /// BOT-2.3: giờ yên lặng theo ca (<see cref="QuietHours"/>) – mỗi người nhận có notBefore riêng; 🔴 không tới người ngoài ca (trừ leo thang).
    /// </summary>
    public class NotificationService(IUnitOfWork unitOfWork, IClock clock, IOptionsMonitor<MessagingOptions> options) : INotificationService
    {
        public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public async Task<List<Guid>> EnqueueAsync(NotificationRequest request, CancellationToken ct = default)
        {
            var template = NotificationCatalog.Find(request.Template)
                ?? throw new ArgumentException(string.Format(ConstantHelper.Message.BotTemplateUnknown, request.Template));
            var db = unitOfWork.DbContext;
            string botCode = string.IsNullOrWhiteSpace(request.BotCode) ? NotificationCatalog.BotFor(template.Scope) : request.BotCode.Trim().ToUpperInvariant();
            var recipients = await RecipientResolver.ResolveAsync(db, botCode, request.UserIds.Distinct().ToList(), request.EmployeeIds.Distinct().ToList(), ct);
            // BOT-1.7d: nhóm chat theo ConfigJson.groups của bot (mẫu không cá nhân / không duyệt) – mode replace: người trên cùng bot không nhận tin riêng.
            var bots = await RecipientResolver.ActiveBotsAsync(db, ct);
            var channels = recipients.Select(r => r.ChannelId).ToHashSet();
            if (bots.TryGetValue(botCode, out Guid target))
            {
                channels.Add(target);
            }
            var groups = await ChatGroups.ForTemplateAsync(db, channels, template, ct);
            var replaced = groups.Where(g => g.Value.Any(x => !x.Copy)).Select(g => g.Key).ToHashSet();
            recipients = [.. recipients.Where(r => !replaced.Contains(r.ChannelId))];
            if (recipients.Count == 0 && groups.Count == 0)
            {
                Log.Information("[Notification] {Template}: không có người nhận đã liên kết bot (users {Users}, employees {Employees})",
                    template.Code, request.UserIds.Count, request.EmployeeIds.Count);
                return [];
            }
            DateTime now = clock.Now;
            var quiet = await QuietHours.PlanAsync(db, recipients, template.Level, now, options.CurrentValue.QuietHours, request.Escalation, ct);
            var payloads = new Dictionary<DateTime, string>();
            string PayloadFor(ResolvedRecipient? r)
            {
                DateTime? at = r != null && quiet.For(r.IdentityId) is { } q && (request.NotBefore == null || q > request.NotBefore) ? q : request.NotBefore;
                DateTime key = at ?? DateTime.MinValue;
                if (!payloads.TryGetValue(key, out var json))
                {
                    json = JsonSerializer.Serialize(new
                    {
                        template = template.Code,
                        data = request.Data,
                        buttons = request.Buttons,
                        scope = template.Scope,
                        level = template.Level,
                        // GAP-INT-03: chưa có cột ScheduledAt → hẹn giờ trong PayloadJson (ClaimOutbound lọc bằng JSON_VALUE).
                        notBefore = at?.ToString("yyyy-MM-ddTHH:mm:ss"),
                    }, Json);
                    payloads[key] = json;
                }
                return json;
            }
            var groupMessages = groups.SelectMany(g => g.Value.Select(x => (Channel: g.Key, Group: x))).ToList();
            var keys = recipients.Select(r => KeyFor(request.IdempotencyKey, r.IdentityId))
                .Concat(groupMessages.Select(g => GroupKeyFor(request.IdempotencyKey, g.Group))).Where(k => k != null).Cast<string>().ToList();
            var existing = keys.Count == 0 ? [] : await db.OutboundMessage.AsNoTracking()
                .Where(m => !m.IsDeleted && m.IdempotencyKey != null && keys.Contains(m.IdempotencyKey))
                .Select(m => m.IdempotencyKey!).ToListAsync(ct);
            var ids = new List<Guid>();
            foreach (var r in recipients.Where(r => !quiet.Skip.Contains(r.IdentityId)))
            {
                string? key = KeyFor(request.IdempotencyKey, r.IdentityId);
                if (key != null && existing.Contains(key))
                {
                    continue;
                }
                var message = new OutboundMessage
                {
                    Id = Guid.NewGuid(),
                    IntegrationChannelId = r.ChannelId,
                    ExternalConversationId = r.ExternalUserId,
                    RecipientExternalId = r.ExternalUserId,
                    MessageType = template.Code,
                    PayloadJson = PayloadFor(r),
                    QueuedAt = now,
                    MessageStatus = MessagingCode.Outbound.Queued,
                    RetryCount = 0,
                    IdempotencyKey = key,
                    CorrelationId = MessagingRules.Truncate(request.CorrelationId, 100),
                };
                unitOfWork.OutboundMessageRepository.Add(message);
                ids.Add(message.Id);
            }
            foreach (var (channel, group) in groupMessages)
            {
                string? key = GroupKeyFor(request.IdempotencyKey, group);
                if (key != null && existing.Contains(key))
                {
                    continue;
                }
                // Nhóm: không xét giờ yên lặng theo ca (tin chung của bộ phận); chỉ hẹn giờ người gọi truyền.
                var message = new OutboundMessage
                {
                    Id = Guid.NewGuid(), IntegrationChannelId = channel, ExternalConversationId = group.ChatId, RecipientExternalId = group.ChatId,
                    MessageType = template.Code, PayloadJson = PayloadFor(null), QueuedAt = now, MessageStatus = MessagingCode.Outbound.Queued, RetryCount = 0,
                    IdempotencyKey = key, CorrelationId = MessagingRules.Truncate(request.CorrelationId, 100),
                };
                unitOfWork.OutboundMessageRepository.Add(message);
                ids.Add(message.Id);
            }
            if (ids.Count > 0)
            {
                // Cùng transaction của người gọi (UnitOfWork dùng chung kết nối DbContext).
                await unitOfWork.OutboundMessageRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
            }
            return ids;
        }

        private static string? GroupKeyFor(string? key, ChatGroup group)
            => string.IsNullOrWhiteSpace(key) ? null : MessagingRules.Truncate(key.Trim(), 160) + ":G:" + MessagingRules.Truncate(group.Code, 32);

        private static string? KeyFor(string? key, Guid identityId)
            => string.IsNullOrWhiteSpace(key) ? null : MessagingRules.Truncate(key.Trim(), 160) + ":" + identityId.ToString("N");
    }
}
