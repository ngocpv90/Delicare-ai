using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Channels.Telegram;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Presentation;
using Microsoft.Extensions.Logging;

namespace AppDelicare.Messaging.Experience
{
    /// <summary>
    /// BOT-1.4 (BOT_TELEGRAM §1.2): Thẻ việc – thông báo kèm nút làm luôn. Khi việc đã xong (đã duyệt / đã nhận / đã làm), thẻ ở MỌI người nhận cùng sự việc
    /// (cùng <c>CorrelationId</c> – api <c>BotChannel/GetCorrelated</c>) đổi thành "✅ … bởi &lt;tên&gt; – HH:mm", nút biến mất.
    /// Mã sự việc của duyệt: <c>APR:&lt;Id yêu cầu dạng N&gt;</c> (NotificationService.Enqueue CorrelationId – BOT-1.7 phát thẻ khi gửi duyệt).
    /// </summary>
    public sealed class TaskCard(IBotChannelApi api, IChannelSender sender, ILogger<TaskCard> logger)
    {
        public static string ApprovalCorrelation(Guid approvalId) => "APR:" + approvalId.ToString("N");

        /// <summary>Sửa mọi thẻ cùng sự việc (trừ tin <paramref name="skipMessageId"/> của chính người bấm – đã sửa riêng). Trả số thẻ đã sửa.</summary>
        public async Task<int> CloseCorrelatedAsync(string correlationId, string doneText, string? skipChatId = null, string? skipMessageId = null, CancellationToken ct = default)
        {
            var messages = await api.GetCorrelatedAsync(correlationId, ct);
            int done = 0;
            string html = TelegramFormatter.Render(NeutralMessage.Text(doneText)).Html;
            foreach (var m in messages.Where(m => m.BotCode != null && m.ExternalConversationId != null && m.ProviderMessageId != null))
            {
                if (m.ExternalConversationId == skipChatId && m.ProviderMessageId == skipMessageId)
                {
                    continue;
                }
                var r = await sender.EditAsync(m.BotCode!, m.ExternalConversationId!, m.ProviderMessageId!, html, null, ct);
                if (r.Status == ChannelSendStatus.Sent)
                {
                    done++;
                }
                else
                {
                    logger.LogInformation("Thẻ {Id} không sửa được ({Status}: {Error})", m.Id, r.Status, r.Error);
                }
            }
            return done;
        }
    }
}
