using System.Text.Json;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement.Messaging
{
    /// <summary>
    /// BOT-1.1 (D-62): outbox. Service nghiệp vụ gọi <see cref="EnqueueAsync"/> TRONG transaction đang mở của mình → tin chỉ tồn tại khi nghiệp vụ commit
    /// (rollback → không có OutboundMessage). Lưu mã mẫu + dữ liệu (không câu chữ): <c>PayloadJson = { template, data, buttons, scope, level, notBefore? }</c>.
    /// Mỗi người nhận đã liên kết 1 dòng (bot theo phạm vi mẫu – <see cref="NotificationCatalog.BotFor"/>), chống trùng theo <c>IdempotencyKey:IdentityId</c>.
    /// Người chưa liên kết bot → bỏ qua (ghi log), không làm hỏng nghiệp vụ.
    /// </summary>
    public class NotificationService(IUnitOfWork unitOfWork, IClock clock) : INotificationService
    {
        public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public async Task<List<Guid>> EnqueueAsync(NotificationRequest request, CancellationToken ct = default)
        {
            var template = NotificationCatalog.Find(request.Template)
                ?? throw new ArgumentException(string.Format(ConstantHelper.Message.BotTemplateUnknown, request.Template));
            var db = unitOfWork.DbContext;
            string botCode = string.IsNullOrWhiteSpace(request.BotCode) ? NotificationCatalog.BotFor(template.Scope) : request.BotCode.Trim().ToUpperInvariant();
            var recipients = await RecipientResolver.ResolveAsync(db, botCode, request.UserIds.Distinct().ToList(), request.EmployeeIds.Distinct().ToList(), ct);
            if (recipients.Count == 0)
            {
                Log.Information("[Notification] {Template}: không có người nhận đã liên kết bot (users {Users}, employees {Employees})",
                    template.Code, request.UserIds.Count, request.EmployeeIds.Count);
                return [];
            }
            DateTime now = clock.Now;
            string payload = JsonSerializer.Serialize(new
            {
                template = template.Code,
                data = request.Data,
                buttons = request.Buttons,
                scope = template.Scope,
                level = template.Level,
                // GAP-INT-03: chưa có cột ScheduledAt → hẹn giờ trong PayloadJson (ClaimOutbound lọc bằng JSON_VALUE).
                notBefore = request.NotBefore?.ToString("yyyy-MM-ddTHH:mm:ss"),
            }, Json);
            var keys = recipients.Select(r => KeyFor(request.IdempotencyKey, r.IdentityId)).Where(k => k != null).Cast<string>().ToList();
            var existing = keys.Count == 0 ? [] : await db.OutboundMessage.AsNoTracking()
                .Where(m => !m.IsDeleted && m.IdempotencyKey != null && keys.Contains(m.IdempotencyKey))
                .Select(m => m.IdempotencyKey!).ToListAsync(ct);
            var ids = new List<Guid>();
            foreach (var r in recipients)
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
                    PayloadJson = payload,
                    QueuedAt = now,
                    MessageStatus = MessagingCode.Outbound.Queued,
                    RetryCount = 0,
                    IdempotencyKey = key,
                    CorrelationId = MessagingRules.Truncate(request.CorrelationId, 100),
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

        private static string? KeyFor(string? key, Guid identityId)
            => string.IsNullOrWhiteSpace(key) ? null : MessagingRules.Truncate(key.Trim(), 160) + ":" + identityId.ToString("N");
    }
}
