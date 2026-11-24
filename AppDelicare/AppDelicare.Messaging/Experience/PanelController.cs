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
    /// BOT-UX-2: ngăn xếp theo MÀN – mỗi màn xem (thẻ, chọn giá trị – <see cref="Screens"/>) là 1 bước; ◀ mở lại đúng màn trước bằng callback chỉ xem của màn đó
    /// (không bao giờ chạy lại thao tác ghi); bàn phím dưới 3 phím gửi lại khi /menu, /ca, /help và lần đầu của chat sau khi container khởi động.
    /// </summary>
    public sealed class PanelController(PanelRegistry registry, IChannelSender sender, ReplyContextStore replies, BotActionRegistry actions,
        IOptionsMonitor<PanelOptions> options)
    {
        public const string KeyboardHint = "⌨️ Phím dưới: " + KeyboardBuilder.MyShift + " · " + KeyboardBuilder.Functions + " · " + KeyboardBuilder.CheckIn;

        public bool Enabled => options.CurrentValue.Enabled && sender is PanelSender;

        private PanelSender Panel => (PanelSender)sender;

        public PanelState State(string bot, string chatId) => registry.Get(bot, chatId);

        /// <summary>Nút bấm trên tin KHÔNG phải panel hiện tại nhưng là panel cũ (hoặc nút menu / điều hướng / trang – chỉ có trên panel) → cũ.</summary>
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
            return state.OldPanels.Contains(id) || kind is CallbackData.Kinds.Menu or CallbackData.Kinds.Nav or CallbackData.Kinds.Page;
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

        /// <summary>
        /// BOT-UX-2 (mục 4): bàn phím dưới [⚡ Ca của tôi] [📂 Chức năng] [📍 Chấm công]. Telegram chỉ gắn bàn phím dưới vào 1 tin riêng (không gắn được vào panel có nút inline)
        /// → gửi 1 tin ngắn mang bàn phím (trước panel) rồi xóa tin bàn phím cũ. <paramref name="force"/> = /menu, /ca, /help; không ép thì chỉ gửi lần đầu của chat
        /// sau khi container khởi động (trạng thái trong bộ nhớ).
        /// </summary>
        public async Task EnsureKeyboardAsync(string bot, string chatId, bool force, CancellationToken ct)
        {
            var state = registry.Get(bot, chatId);
            if (!force && state.KeyboardShown)
            {
                return;
            }
            var sent = await Panel.Inner.SendAsync(bot, new OutgoingMessage(chatId, KeyboardHint, KeyboardBuilder.MainMenu()), ct);
            if (sent.ProviderMessageId == null)
            {
                return;
            }
            string? old = state.KeyboardMessageId;
            state.KeyboardMessageId = sent.ProviderMessageId;
            state.KeyboardShown = true;
            if (old != null)
            {
                await Panel.Inner.DeleteAsync(bot, chatId, old, ct);
            }
        }

        /// <summary>Bàn phím dưới vừa được gửi ngoài panel (lời chào khi liên kết / /start).</summary>
        public void KeyboardSent(string bot, string chatId) => registry.Get(bot, chatId).KeyboardShown = true;

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
            state.TopShown = true;
        }

        public static void EnterModule(PanelState state, string module)
        {
            EnterRoot(state);
            state.Stack.Add(new PanelEntry(PanelLevel.Module, module, BotActionRegistry.ModuleLabel(module)));
        }

        /// <summary>
        /// Mở 1 chức năng: Menu › Phân hệ › Chức năng + ghi ⭐ hay dùng. Bỏ bước phân hệ khi màn đầu phẳng (≤ 6 chức năng – BOT-UX-2 mục 12) hoặc người đó chỉ có 1 phân hệ.
        /// </summary>
        public void EnterAction(PanelState state, BotActionContext ctx, BotAction action)
        {
            var menu = actions.MenuAvailable(ctx.Bot, ctx.Identity);
            bool flat = menu.Count <= KeyboardBuilder.FlatMax || menu.Select(a => a.Module).Distinct().Count() <= 1;
            if (flat || !menu.Contains(action))
            {
                EnterRoot(state);
            }
            else
            {
                EnterModule(state, action.Module);
            }
            state.Stack.Add(new PanelEntry(PanelLevel.Action, action.Code, action.Label));
            state.Touch(action.Code, options.CurrentValue.RecentCount);
            if (PanelSession.Current is { } s)
            {
                s.EnteredAction = true;
            }
        }

        /// <summary>Màn không thuộc danh mục chức năng (tra mã, trợ giúp) – Quay lại về Menu.</summary>
        public static void EnterLoose(PanelState state, string label)
        {
            EnterRoot(state);
            state.Stack.Add(new PanelEntry(PanelLevel.Action, null, label));
            if (PanelSession.Current is { } s)
            {
                s.EnteredAction = true;
            }
        }

        /// <summary>
        /// BOT-UX-2 (mục 2): ghi màn vừa vẽ vào ngăn xếp. Màn = callback chỉ xem vừa bấm (<see cref="Screens.IsView(string?)"/>) hoặc thẻ tự khai báo (<see cref="Screens.Show"/>).
        /// Màn đã có trong ngăn xếp (cùng khóa) → lùi về đó; chưa có → đẩy thêm; thẻ chức năng tự mở ngay khi vào chức năng → gộp vào bước chức năng.
        /// Không có màn (thao tác ghi chỉ báo chữ, câu hỏi gõ tay, lỗi) → panel đang hiện màn tạm (◀ mở lại màn đầu ngăn xếp).
        /// </summary>
        public static void Commit(PanelSession session)
        {
            var (stack, shown) = Plan(session);
            session.State.Stack.Clear();
            session.State.Stack.AddRange(stack);
            if (shown != null)
            {
                session.State.TopShown = shown.Value;
            }
        }

        /// <summary>
        /// Ngăn xếp sau lần xử lý này (không đổi trạng thái) – PanelSender dùng để vẽ đường dẫn đúng ngay lần vẽ panel đầu tiên, <see cref="Commit"/> dùng để ghi.
        /// <c>TopShown</c> null = giữ nguyên.
        /// </summary>
        public static (List<PanelEntry> Stack, bool? TopShown) Plan(PanelSession session)
        {
            var stack = session.State.Stack.ToList();
            string? callback = session.Callback;
            string? kind = CallbackData.Parse(callback)?.Kind;
            bool structural = kind is CallbackData.Kinds.Menu or CallbackData.Kinds.Nav or CallbackData.Kinds.Page;
            var declared = session.Screen ?? (callback != null && Screens.IsView(callback) ? (callback, Screens.KeyOf(callback)) : null);
            string? label = session.Parts.Select(p => Screens.LabelOf(p.Html)).FirstOrDefault(l => l != null);
            int action = stack.FindLastIndex(e => e.Level == PanelLevel.Action);
            if (declared is { } d && label != null && action >= 0)
            {
                if (session.EnteredAction)
                {
                    Truncate(stack, action);
                    stack[action] = stack[action] with { Detail = label };
                }
                else
                {
                    int i = stack.FindIndex(e => e.Level == PanelLevel.Screen && e.Key == d.Key);
                    if (i >= 0)
                    {
                        Truncate(stack, i);
                        stack[i] = stack[i] with { Label = label };
                    }
                    else
                    {
                        stack.Add(new PanelEntry(PanelLevel.Screen, null, label, d.Replay, d.Key));
                    }
                }
                return (stack, true);
            }
            return (stack, !structural && !session.EnteredAction ? false : null);
        }

        private static void Truncate(PanelState state, int keep) => Truncate(state.Stack, keep);

        private static void Truncate(List<PanelEntry> stack, int keep)
        {
            if (keep >= 0 && stack.Count > keep + 1)
            {
                stack.RemoveRange(keep + 1, stack.Count - keep - 1);
            }
        }

        /// <summary>
        /// Nút điều hướng <c>nav:*</c> trên panel. ◀ Quay lại: panel đang hiện màn tạm (câu hỏi gõ tay, lỗi, kết quả) → mở lại màn đầu ngăn xếp; không thì bỏ màn đầu và mở màn trước.
        /// Màn được mở lại bằng callback CHỈ XEM của nó qua <paramref name="replay"/> (bộ định tuyến); chức năng chạy lại như mở mới; phân hệ / menu vẽ lại.
        /// </summary>
        public async Task NavAsync(BotActionContext ctx, CallbackData data, MenuNavigator menu, Func<BotActionContext, CancellationToken, Task> replay, CancellationToken ct)
        {
            var state = registry.Get(ctx.Bot, ctx.Event.ChatId);
            await CancelDraftAsync(ctx.Bot, ctx.Event.ChatId, state, ct);
            switch (data.Action)
            {
                case PanelNav.Close:
                    await Panel.CloseAsync(ctx.Bot, ctx.Event.ChatId, state, ct);
                    state.Stack.Clear();
                    state.TopShown = true;
                    CallbackAnswer.Set("Đã đóng menu");
                    return;
                case PanelNav.Home:
                    await menu.OpenAsync(ctx, ct);
                    CallbackAnswer.Set("🏠 Menu");
                    return;
                case PanelNav.Continue:
                    int a = state.Stack.FindLastIndex(e => e.Level == PanelLevel.Action);
                    if (a < 0)
                    {
                        await menu.OpenAsync(ctx, ct);
                        return;
                    }
                    Truncate(state, a);
                    await ShowTopAsync(ctx, state, menu, replay, ct);
                    return;
                case PanelNav.Back:
                    if (state.Top is not { Level: not PanelLevel.Root })
                    {
                        await menu.OpenAsync(ctx, ct);
                        return;
                    }
                    if (state.TopShown)
                    {
                        state.Stack.RemoveAt(state.Stack.Count - 1);
                    }
                    await ShowTopAsync(ctx, state, menu, replay, ct);
                    return;
                default:
                    await menu.OpenAsync(ctx, ct);
                    return;
            }
        }

        /// <summary>Vẽ lại màn đầu ngăn xếp (Quay lại / Tiếp tục) – chỉ bằng callback xem hoặc chạy lại chức năng như mở mới.</summary>
        private async Task ShowTopAsync(BotActionContext ctx, PanelState state, MenuNavigator menu, Func<BotActionContext, CancellationToken, Task> replay, CancellationToken ct)
        {
            var top = state.Top;
            switch (top?.Level)
            {
                case PanelLevel.Module when top.Id != null:
                    await menu.ShowModuleAsync(ctx, top.Id, ct);
                    return;
                case PanelLevel.Action when top.Id != null && actions.ByCode(top.Id) is { } action && action.IsAllowed(ctx.Bot, ctx.Identity):
                    await RerunAsync(ctx, state, action, top, ct);
                    return;
                case PanelLevel.Screen when Screens.IsView(top.Replay):
                    var session = PanelSession.Current;
                    if (session != null)
                    {
                        session.Callback = top.Replay;
                        session.Screen = null;
                    }
                    await replay(ViewContext(ctx, top.Replay), ct);
                    if (session != null)
                    {
                        Commit(session);
                    }
                    return;
                case PanelLevel.Action or PanelLevel.Screen:
                    // Màn tra cứu / trợ giúp (không có callback xem) → bỏ và về màn trước.
                    state.Stack.RemoveAt(state.Stack.Count - 1);
                    await ShowTopAsync(ctx, state, menu, replay, ct);
                    return;
                default:
                    await menu.OpenAsync(ctx, ct);
                    return;
            }
        }

        /// <summary>Chạy lại chức năng như mở mới (giữ trang / bộ lọc danh sách đang xem); thẻ tự mở được gộp lại vào bước chức năng.</summary>
        private async Task RerunAsync(BotActionContext ctx, PanelState state, BotAction action, PanelEntry entry, CancellationToken ct)
        {
            int i = state.Stack.LastIndexOf(entry);
            Truncate(state, i);
            state.Stack[i] = entry with { Detail = null };
            state.TopShown = true;
            var session = PanelSession.Current;
            if (session != null)
            {
                session.EnteredAction = true;
                session.Callback = null;
                session.Screen = null;
            }
            var (page, filter) = ListPager.Parse(entry.Replay);
            await action.Handler(ViewContext(ctx, null) with { Page = page, Filter = filter }, ct);
            if (session != null)
            {
                Commit(session);
            }
        }

        /// <summary>BOT-UX-2 (mục 10): nút trang / bộ lọc danh sách <c>pg:&lt;mã chức năng&gt;:&lt;trang&gt;[.&lt;lọc&gt;]</c> – chạy lại chức năng (chỉ đọc) ở trang đó.</summary>
        public async Task PageAsync(BotActionContext ctx, CallbackData data, CancellationToken ct)
        {
            var action = actions.ByCode(data.Action);
            if (action == null || !action.IsAllowed(ctx.Bot, ctx.Identity))
            {
                await BotReply.TextAsync(sender, ctx.Bot, ctx.Event.ChatId, "Chức năng không còn trong quyền của bạn.", ct: ct);
                return;
            }
            var (page, filter) = ListPager.Parse(ctx.Event.CallbackData);
            if (PanelSession.Current is { } s)
            {
                var state = s.State;
                int i = state.Stack.FindLastIndex(e => e.Level == PanelLevel.Action && e.Id == action.Code);
                if (i < 0)
                {
                    EnterAction(state, ctx, action);
                    i = state.Stack.Count - 1;
                }
                Truncate(state, i);
                state.Stack[i] = state.Stack[i] with { Replay = ctx.Event.CallbackData, Detail = null };
                state.TopShown = true;
                s.EnteredAction = true;
            }
            CallbackAnswer.Set(filter == ListPager.All ? $"Tất cả · trang {page + 1}" : $"Trang {page + 1}");
            await action.Handler(ctx with { Page = page, Filter = filter }, ct);
        }

        private static BotActionContext ViewContext(BotActionContext ctx, string? callback)
            => ctx with { Event = ctx.Event with { Kind = IncomingKind.Callback, CallbackData = callback, CallbackId = null, Command = null, Argument = null }, Page = 0, Filter = null };
    }
}
