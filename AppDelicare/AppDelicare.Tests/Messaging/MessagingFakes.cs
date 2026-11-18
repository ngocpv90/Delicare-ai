using System.Collections.Concurrent;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;

namespace AppDelicare.Tests.Messaging
{
    /// <summary>api giả cho container messaging: ghi lại mọi lệnh, trả kết quả cấu hình được; <see cref="Down"/> = api tắt (ném ApiUnavailableException).</summary>
    public sealed class FakeBotChannelApi : IBotChannelApi
    {
        public bool Down { get; set; }
        public ConcurrentQueue<BotUpdateRequest> Received { get; } = new();
        public ConcurrentQueue<(Guid Id, string Status, string? Error)> Completed { get; } = new();
        public ConcurrentQueue<BotMarkOutboundRequest> Marked { get; } = new();
        public ConcurrentQueue<object> Calls { get; } = new();
        public List<BotInboundItemModel> InboundQueue { get; } = [];
        public List<BotOutboundItemModel> OutboundQueue { get; } = [];
        public BotIdentityModel Identity { get; set; } = new();
        public Func<object, ApiResult<BotIdentityModel>>? LinkResult { get; set; }
        public ApiResult<UserTokenModel> Token { get; set; } = new() { ResponseCode = 400, ResponseMessage = "Tài khoản Telegram chưa liên kết" };

        private void Guard()
        {
            if (Down)
            {
                throw new ApiUnavailableException("api tắt (giả)");
            }
        }

        public Task<ApiResult<BotUpdateResultModel>> ReceiveUpdateAsync(BotUpdateRequest request, CancellationToken ct = default)
        {
            Guard();
            Received.Enqueue(request);
            return Task.FromResult(new ApiResult<BotUpdateResultModel> { ResponseCode = 200, Data = new BotUpdateResultModel { Id = Guid.NewGuid() } });
        }

        public Task<List<BotInboundItemModel>> ClaimInboundAsync(string? botCode, int max, CancellationToken ct = default)
        {
            Guard();
            lock (InboundQueue)
            {
                var items = InboundQueue.Take(max).ToList();
                InboundQueue.RemoveRange(0, items.Count);
                return Task.FromResult(items);
            }
        }

        public Task CompleteInboundAsync(Guid id, string status, string? error = null, CancellationToken ct = default)
        {
            Guard();
            Completed.Enqueue((id, status, error));
            return Task.CompletedTask;
        }

        public Task<List<BotOutboundItemModel>> ClaimOutboundAsync(string? botCode, int max, CancellationToken ct = default)
        {
            Guard();
            lock (OutboundQueue)
            {
                var items = OutboundQueue.Where(o => botCode == null || o.BotCode == botCode).Take(max).ToList();
                OutboundQueue.RemoveAll(items.Contains);
                return Task.FromResult(items);
            }
        }

        public Task MarkOutboundAsync(BotMarkOutboundRequest request, CancellationToken ct = default)
        {
            Guard();
            Marked.Enqueue(request);
            return Task.CompletedTask;
        }

        public Task<ApiResult<BotIdentityModel>> ResolveIdentityAsync(BotIdentityRequest request, CancellationToken ct = default)
            => Ok(request, Identity);

        public Task<ApiResult<BotIdentityModel>> LinkByPhoneAsync(BotLinkByPhoneRequest request, CancellationToken ct = default)
            => Task.FromResult(Record(request, LinkResult?.Invoke(request) ?? new ApiResult<BotIdentityModel> { ResponseCode = 200, Data = Identity }));

        public Task<ApiResult<BotIdentityModel>> LinkByCodeAsync(BotLinkByCodeRequest request, CancellationToken ct = default)
            => Task.FromResult(Record(request, LinkResult?.Invoke(request) ?? new ApiResult<BotIdentityModel> { ResponseCode = 200, Data = Identity }));

        public Task<ApiResult<BotIdentityModel>> TouchIdentityAsync(BotTouchRequest request, CancellationToken ct = default)
            => Ok(request, Identity);

        public Task<ApiResult<bool>> UnlinkIdentityAsync(BotIdentityRequest request, CancellationToken ct = default)
            => Task.FromResult(Record(request, new ApiResult<bool> { ResponseCode = 200, Data = true }));

        public Task<ApiResult<UserTokenModel>> IssueUserTokenAsync(BotIdentityRequest request, CancellationToken ct = default)
            => Task.FromResult(Record(request, Token));

        public MyAttendanceModel Today { get; set; } = new() { EmployeeName = "NV thử" };
        public List<BotCorrelatedMessageModel> Correlated { get; } = [];
        public ConcurrentQueue<(string Action, string Json)> EmployeeCalls { get; } = new();

        public Task<ApiResult<MyAttendanceModel>> EmployeeGetTodayAsync(BotIdentityRequest request, CancellationToken ct = default)
            => Task.FromResult(Record(request, new ApiResult<MyAttendanceModel> { ResponseCode = 200, Data = Today }));

        public List<ReferenceItemModel> LeaveTypes { get; } = [new() { Code = "ANNUAL", Label = "Nghỉ phép năm" }, new() { Code = "SICK", Label = "Nghỉ ốm" }];

        public Task<List<ReferenceItemModel>> GetLeaveTypesAsync(CancellationToken ct = default) => Task.FromResult(LeaveTypes.ToList());

        public Task<List<BotCorrelatedMessageModel>> GetCorrelatedAsync(string correlationId, CancellationToken ct = default)
        {
            Guard();
            Calls.Enqueue("corr:" + correlationId);
            return Task.FromResult(Correlated.ToList());
        }

        // BOT-2.1: Thẻ ca.
        public List<ShiftCardItemModel> ShiftCards { get; } = [];
        public ApiResult<ShiftCardItemModel> MyCard { get; set; } = new() { ResponseCode = 400, ResponseMessage = "Hôm nay bạn không có ca." };

        public Task<List<ShiftCardItemModel>> SyncShiftCardsAsync(CancellationToken ct = default)
        {
            Guard();
            return Task.FromResult(ShiftCards.ToList());
        }

        public Task<ApiResult<ShiftCardItemModel>> MyShiftCardAsync(BotIdentityRequest request, CancellationToken ct = default)
        {
            Guard();
            return Task.FromResult(MyCard);
        }

        public Task<ApiResult<System.Text.Json.JsonElement>> EmployeeCallAsync(string action, object request, CancellationToken ct = default)
        {
            Guard();
            EmployeeCalls.Enqueue((action, System.Text.Json.JsonSerializer.Serialize(request)));
            return Task.FromResult(new ApiResult<System.Text.Json.JsonElement> { ResponseCode = 200, ResponseMessage = "Đã ghi (giả)", ListData = [] });
        }

        private Task<ApiResult<BotIdentityModel>> Ok(object request, BotIdentityModel identity)
        {
            Guard();
            return Task.FromResult(Record(request, new ApiResult<BotIdentityModel> { ResponseCode = 200, Data = identity }));
        }

        private T Record<T>(object request, T result)
        {
            Guard();
            Calls.Enqueue(request);
            return result;
        }
    }

    /// <summary>Kênh giả: ghi lại tin gửi; <see cref="Next"/> = kết quả lần gửi kế (mặc định Sent).</summary>
    public sealed class FakeSender : IChannelSender
    {
        public ConcurrentQueue<(string Bot, OutgoingMessage Message)> Sent { get; } = new();
        public ConcurrentQueue<(string Bot, string Chat, string MessageId, string Text, object? Markup)> Edits { get; } = new();
        public Queue<ChannelSendResult> Next { get; } = new();
        public int Answered;
        private int _id = 100;

        public Task<ChannelSendResult> SendAsync(string botCode, OutgoingMessage message, CancellationToken ct = default)
        {
            Sent.Enqueue((botCode, message));
            return Task.FromResult(Next.Count > 0 ? Next.Dequeue() : ChannelSendResult.Sent((++_id).ToString()));
        }

        public Task<ChannelSendResult> EditAsync(string botCode, string chatId, string providerMessageId, string text, object? replyMarkup, CancellationToken ct = default)
        {
            Edits.Enqueue((botCode, chatId, providerMessageId, text, replyMarkup));
            return Task.FromResult(ChannelSendResult.Sent(providerMessageId));
        }

        public ConcurrentQueue<(string Bot, string Chat, string MessageId)> Pins { get; } = new();

        public Task<ChannelSendResult> PinAsync(string botCode, string chatId, string providerMessageId, CancellationToken ct = default)
        {
            Pins.Enqueue((botCode, chatId, providerMessageId));
            return Task.FromResult(ChannelSendResult.Sent(providerMessageId));
        }

        public Task AnswerCallbackAsync(string botCode, string callbackId, string? text = null, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Answered);
            return Task.CompletedTask;
        }
    }
}
