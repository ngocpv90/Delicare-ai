using AppDelicare.Messaging.Audiences.Internal;
using AppDelicare.Messaging.Audiences.Internal.Chung;
using AppDelicare.Messaging.Callback;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Channels.Telegram;
using AppDelicare.Messaging.Conversations;
using Microsoft.Extensions.Options;

namespace AppDelicare.Messaging.Experience
{
    /// <summary>
    /// BOT-UX-1: điều khiển bảng điều khiển duy nhất của chat riêng – mở phiên (<see cref="PanelSession"/>), ngăn xếp điều hướng (breadcrumb),
    /// [◀ Quay lại] [🏠 Menu] [✖ Đóng] [▶ Tiếp tục], hủy nháp đang nhập, nhận biết nút trên panel cũ ("Menu này đã cũ").
    /// </summary>
    public sealed class PanelController(PanelRegistry registry, IChannelSender sender, ReplyContextStore replies, BotActionRegistry actions,
        IOptionsMonitor<PanelOptions> options)
    {
        public bool Enabled => options.CurrentValue.Enabled && sender is PanelSender;

        private PanelSender Panel => (PanelSender)sender;

        public PanelState State(string bot, string chatId) => registry.Get(bot, chatId);

        /// <summary>Nút bấm trên tin KHÔNG phải panel hiện tại nhưng là panel cũ (hoặc nút menu / điều hướng – chỉ có trên panel) → cũ.</summary>
        public bool IsStale(string bot, IncomingEvent e)
        {
            if (!Enabled || e.Kind != IncomingKind.Callback || e.MessageId is not { } mid)
            {
                return false;
            }
            string id = mid.ToString();
            var state = registry.Get(bot, e.ChatId);
            if (id == state.MessageId)
            {
                return false;
            }
            string? kind = CallbackData.Parse(e.CallbackData)?.Kind;
            return state.OldPanels.Contains(id) || kind is CallbackData.Kinds.Menu or CallbackData.Kinds.Nav;
        }

        public bool IsCurrentPanel(string bot, IncomingEvent e)
            => Enabled && e.MessageId is { } mid && registry.Get(bot, e.ChatId).MessageId == mid.ToString();

        /// <summary>Nút trên panel cũ: báo "Menu này đã cũ", xóa tin đó (quá hạn → "Menu đã đóng").</summary>
        public async Task RetireStaleAsync(string bot, IncomingEvent e, CancellationToken ct)
        {
            if (e.CallbackId != null)
            {
                await sender.AnswerCallbackAsync(bot, e.CallbackId, "Menu này đã cũ", ct);
            }
            if (e.MessageId is { } mid)
            {
                await Panel.RemoveAsync(bot, e.ChatId, mid.ToString(), null, ct);
            }
        }

        /// <summary>Chạy <paramref name="work"/> trong 1 phiên panel (tuần tự theo chat).</summary>
        public async Task RunAsync(string bot, string chatId, PanelMode mode, Func<PanelSession, Task> work, CancellationToken ct)
        {
            var state = registry.Get(bot, chatId);
            await state.Gate.WaitAsync(ct);
            var session = PanelSession.Begin(bot, chatId, state, mode);
            try
            {
                await work(session);
            }
            finally
            {
                session.End();
                state.Gate.Release();
            }
        }

        /// <summary>Xóa tin của người dùng / câu hỏi đã xong (tin lệnh, câu trả lời ForceReply). Lỗi bỏ qua.</summary>
        public Task DeleteAsync(string bot, string chatId, long? messageId, CancellationToken ct)
            => messageId is { } id ? Panel.Inner.DeleteAsync(bot, chatId, id.ToString(), ct) : Task.CompletedTask;

        /// <summary>Đã nhận câu trả lời ForceReply: xóa câu hỏi + câu trả lời (panel được cập nhật bởi chức năng).</summary>
        public async Task AnsweredAsync(string bot, IncomingEvent e, CancellationToken ct)
        {
            var state = registry.Get(bot, e.ChatId);
            if (e.ReplyToMessageId is { } q)
            {
                state.DraftQuestions.Remove(q.ToString());
                await DeleteAsync(bot, e.ChatId, q, ct);
            }
            await DeleteAsync(bot, e.ChatId, e.MessageId, ct);
        }

        /// <summary>Hủy nháp đang nhập: bỏ mọi câu hỏi ForceReply đang chờ (không gọi api nào) + xóa tin hỏi.</summary>
        public async Task CancelDraftAsync(string bot, string chatId, PanelState state, CancellationToken ct)
        {
            var ids = replies.Forget(bot, chatId).Concat(state.DraftQuestions).Distinct().ToList();
            state.DraftQuestions.Clear();
            foreach (var id in ids)
            {
                await Panel.Inner.DeleteAsync(bot, chatId, id, ct);
            }
        }

        public static void EnterRoot(PanelState state)
        {
            state.Stack.Clear();
            state.Stack.Add(new PanelEntry(PanelLevel.Root, null, "Menu"));
            state.Depth = 0;
        }

        public static void EnterModule(PanelState state, string module)
        {
            EnterRoot(state);
            state.Stack.Add(new PanelEntry(PanelLevel.Module, module, BotActionRegistry.ModuleLabel(module)));
        }

        /// <summary>Mở 1 chức năng: Menu › Phân hệ › Chức năng (phân hệ bỏ khi người đó chỉ có 1 phân hệ) + ghi ⭐ hay dùng.</summary>
        public void EnterAction(PanelState state, BotActionContext ctx, BotAction action)
        {
            bool single = actions.Available(ctx.Bot, ctx.Identity).Select(a => a.Module).Distinct().Count() <= 1;
            if (single)
            {
                EnterRoot(state);
            }
            else
            {
                EnterModule(state, action.Module);
            }
            state.Stack.Add(new PanelEntry(PanelLevel.Action, action.Code, action.Label));
            state.Touch(action.Code, options.CurrentValue.RecentCount);
        }

        /// <summary>Màn không thuộc danh mục chức năng (tra mã, trợ giúp) – Quay lại về Menu.</summary>
        public static void EnterLoose(PanelState state, string label)
        {
            EnterRoot(state);
            state.Stack.Add(new PanelEntry(PanelLevel.Action, null, label));
        }

        /// <summary>Nút điều hướng <c>nav:*</c> trên panel.</summary>
        public async Task NavAsync(BotActionContext ctx, CallbackData data, MenuNavigator menu, CancellationToken ct)
        {
            var state = registry.Get(ctx.Bot, ctx.Event.ChatId);
            await CancelDraftAsync(ctx.Bot, ctx.Event.ChatId, state, ct);
            switch (data.Action)
            {
                case PanelNav.Close:
                    await Panel.CloseAsync(ctx.Bot, ctx.Event.ChatId, state, ct);
                    state.Stack.Clear();
                    state.Depth = 0;
                    return;
                case PanelNav.Home:
                    await menu.OpenAsync(ctx, ct);
                    return;
                case PanelNav.Continue:
                    await RerunAsync(ctx, state, menu, ct);
                    return;
                case PanelNav.Back:
                    if (state.Depth > 0 && state.CurrentAction != null)
                    {
                        await RerunAsync(ctx, state, menu, ct);
                        return;
                    }
                    var top = state.Top;
                    if (top?.Level == PanelLevel.Action)
                    {
                        state.Stack.RemoveAt(state.Stack.Count - 1);
                        if (state.Top is { Level: PanelLevel.Module, Id: { } module })
                        {
                            await menu.ShowModuleAsync(ctx, module, ct);
                            return;
                        }
                    }
                    await menu.OpenAsync(ctx, ct);
                    return;
                default:
                    await menu.OpenAsync(ctx, ct);
                    return;
            }
        }

        /// <summary>Về màn đầu của chức năng đang dùng (Tiếp tục / Quay lại từ màn con) – chạy lại như mở mới, không lặp lại thao tác ghi.</summary>
        private async Task RerunAsync(BotActionContext ctx, PanelState state, MenuNavigator menu, CancellationToken ct)
        {
            var code = state.CurrentAction?.Id;
            var action = code == null ? null : actions.ByCode(code);
            if (action == null || !action.IsAllowed(ctx.Bot, ctx.Identity))
            {
                await menu.OpenAsync(ctx, ct);
                return;
            }
            state.Depth = 0;
            await action.Handler(ctx with { Event = ctx.Event with { Kind = IncomingKind.Callback, CallbackData = null, Command = null, Argument = null } }, ct);
        }
    }
}
