using System.Threading.Channels;
using AppDelicare.Messaging.Channels.Telegram;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using Microsoft.Extensions.Logging;

namespace AppDelicare.Messaging.Processing
{
    /// <summary>Báo InboundPump có update mới (xử lý ngay, không chờ vòng quét 5″).</summary>
    public sealed class InboundSignal
    {
        private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

        public void Notify() => _channel.Writer.TryWrite(true);

        public async Task WaitAsync(TimeSpan timeout, CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            try
            {
                await _channel.Reader.ReadAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
            }
        }
    }

    public enum IngestResult
    {
        /// <summary>Đã lưu (hoặc trùng – đã lưu trước đó).</summary>
        Stored,
        /// <summary>Update không đọc được / bot chưa khai báo phía api – bỏ, không cho Telegram gửi lại.</summary>
        Rejected,
        /// <summary>api không trả lời – webhook trả 503 để Telegram gửi lại; polling giữ offset.</summary>
        Unavailable,
    }

    /// <summary>BOT-1.3: lưu update thô vào api (BotChannel/ReceiveUpdate – chống trùng theo bot + update_id) rồi báo InboundPump. Dùng chung webhook + polling.</summary>
    public sealed class UpdateIngestor(IBotChannelApi api, InboundSignal signal, ILogger<UpdateIngestor> logger)
    {
        public async Task<IngestResult> IngestAsync(string botCode, string rawJson, CancellationToken ct = default)
        {
            var peek = TelegramUpdateParser.Peek(rawJson);
            if (peek == null)
            {
                logger.LogWarning("Bot {Bot}: update không đọc được – bỏ qua", botCode);
                return IngestResult.Rejected;
            }
            try
            {
                var r = await api.ReceiveUpdateAsync(new BotUpdateRequest
                {
                    BotCode = botCode, UpdateId = peek.Value.UpdateId, ExternalUserId = peek.Value.FromId, ExternalConversationId = peek.Value.ChatId,
                    ExternalMessageId = peek.Value.MessageId, MessageType = peek.Value.Type, PayloadJson = rawJson,
                }, ct);
                if (!r.IsOk)
                {
                    logger.LogWarning("Bot {Bot}: api từ chối update {UpdateId}: {Message}", botCode, peek.Value.UpdateId, r.ResponseMessage);
                    return r.ResponseCode == 401 ? IngestResult.Unavailable : IngestResult.Rejected;
                }
                signal.Notify();
                return IngestResult.Stored;
            }
            catch (ApiUnavailableException ex)
            {
                logger.LogWarning("Bot {Bot}: api không trả lời ({Error}) – chờ Telegram gửi lại", botCode, ex.Message);
                return IngestResult.Unavailable;
            }
        }
    }
}
