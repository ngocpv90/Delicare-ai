using AppDelicare.Data.Connection;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using Serilog;

namespace AppDelicare.Services.Implement.Messaging
{
    /// <summary>Nút trên tin outbox (BOT-1.7): callback theo quy ước bot (≤ 64 byte, vd. "apv:view:&lt;Id N&gt;") hoặc link.</summary>
    public sealed record NotificationButton(string Text, string? Callback = null, string? Url = null);

    /// <summary>
    /// BOT-1.7: điểm phát thông báo nghiệp vụ dùng chung cho mọi Service. Gọi TRONG transaction của nghiệp vụ (tin chỉ tồn tại khi nghiệp vụ commit).
    /// Lỗi khi xếp tin KHÔNG làm hỏng nghiệp vụ (ghi log cảnh báo). Người nhận theo quyền menu (không kèm admin) / theo người cụ thể.
    /// Chống trùng: <c>IdempotencyKey</c> = mã mẫu + mã sự việc (NotificationService thêm hậu tố người nhận).
    /// </summary>
    public static class BusinessNotifier
    {
        /// <summary>Xếp tin, nuốt lỗi (ghi log) – không bao giờ ném ra ngoài nghiệp vụ. Trả số tin đã xếp (0 = không ai đã liên kết bot / lỗi).</summary>
        public static async Task<int> SafeEnqueueAsync(INotificationService notifications, NotificationRequest request)
        {
            if (request.UserIds.Count == 0 && request.EmployeeIds.Count == 0)
            {
                if (NotificationCatalog.IsIncident(request.Template))
                {
                    try { await notifications.RecordIncidentAsync(request); } catch (Exception ex) { Log.Warning(ex, "[AgentEvent] {Key}", request.IdempotencyKey); }
                }
                return 0;
            }
            try
            {
                return (await notifications.EnqueueAsync(request)).Count;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Notification] {Template} {Key}: không xếp được tin – nghiệp vụ vẫn tiếp tục", request.Template, request.IdempotencyKey);
                return 0;
            }
        }

        /// <summary>Gửi cho người có quyền vào các menu (hợp các nhóm, không kèm admin), trừ <paramref name="except"/> (người vừa thao tác).
        /// <paramref name="requireAdd"/> = chỉ người có quyền Thêm (người làm việc đó, không phải chỉ xem); <paramref name="botCode"/> = ép bot đích.</summary>
        public static async Task<int> ToMenusAsync(INotificationService notifications, DatabaseContext db, string template, string[] menus, object data,
            string idempotencyKey, string? correlationId = null, IEnumerable<NotificationButton>? buttons = null, Guid? except = null, bool requireAdd = false, string? botCode = null)
        {
            try
            {
                var users = new HashSet<Guid>();
                foreach (string menu in menus)
                {
                    users.UnionWith(await RecipientResolver.UsersWithMenuOnlyAsync(db, menu, requireAdd));
                }
                if (except is { } e)
                {
                    users.Remove(e);
                }
                return await SafeEnqueueAsync(notifications, new NotificationRequest
                {
                    Template = template, UserIds = [.. users], Data = data,
                    Buttons = NotificationCatalog.IsIncident(template) ? [.. buttons ?? [], .. IncidentButtons] : buttons?.ToList(), IdempotencyKey = idempotencyKey, CorrelationId = correlationId ?? idempotencyKey, BotCode = botCode,
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Notification] {Template} {Key}: không tìm được người nhận", template, idempotencyKey);
                return 0;
            }
        }

        /// <summary>BOT-2.2: nút sự việc (messaging tìm sự việc qua chính tin được bấm).</summary>
        public static readonly NotificationButton[] IncidentButtons =
            [new("🙋 Nhận xử lý", "inc:claim"), new("💬 Ghi chú", "inc:note"), new("✅ Đã xử lý", "inc:done")];

        /// <summary>Callback mở thẻ trên bot (quy ước CallbackData của AppDelicare.Messaging): &lt;kind&gt;:&lt;action&gt;:&lt;Id N&gt;.</summary>
        public static string Callback(string kind, string action, Guid id) => $"{kind}:{action}:{id:N}";
    }
}
