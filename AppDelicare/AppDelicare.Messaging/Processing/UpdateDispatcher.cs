using AppDelicare.Messaging.Audiences.Internal;
using AppDelicare.Messaging.Audiences.Internal.Chung;
using AppDelicare.Messaging.Callback;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Channels.Telegram;
using AppDelicare.Messaging.Conversations;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using AppDelicare.Messaging.Experience;

namespace AppDelicare.Messaging.Processing
{
    /// <summary>Kết quả xử lý 1 update → BotChannel/CompleteInbound.</summary>
    public sealed record DispatchResult(string Status, string? Error = null)
    {
        public static readonly DispatchResult Processed = new("Processed");
        public static readonly DispatchResult Ignored = new("Ignored");
    }

    /// <summary>
    /// BOT-1.3 / 1.4: update → hành động. Thứ tự: chặn / mở bot (my_chat_member) · nhóm chat → bỏ (BOT-2.x) · đăng ký (liên hệ, /start, /huylienket, gõ mã liên kết)
    /// · chưa liên kết → xin số · trả lời ForceReply (lý do từ chối…) · nút trong tin (menu / apv / undo) · vị trí (chấm công) · nút / lệnh của danh mục chức năng
    /// · 📂 Chức năng · gõ mã chứng từ / chữ → tra cứu · còn lại → trợ giúp.
    /// </summary>
    public sealed class UpdateDispatcher(OnboardingActions onboarding, HelpActions help, BotUserContext users, IBotChannelApi api, IChannelSender sender,
        BotActionRegistry registry, MenuNavigator menu, ShiftCards shiftCards, IncidentActions incidents, ApprovalActions approvals, SelfServiceActions self, CodeSearch search, ReplyContextStore replies,
        AppDelicare.Messaging.Conversations.Flows.LeaveRequestFlow leave, AppDelicare.Messaging.Conversations.Flows.ShiftSwapFlow swap, AppDelicare.Messaging.Audiences.Internal.VanHanh.BepActions bep,
        AppDelicare.Messaging.Audiences.Internal.VanHanh.AttpActions attp, AppDelicare.Messaging.Audiences.Internal.VanHanh.GiaoVanActions giaoVan, AppDelicare.Messaging.Audiences.Internal.VanHanh.DiemPhatActions diemPhat,
        AppDelicare.Messaging.Audiences.Internal.VanHanh.PhanAnhActions phanAnh, AppDelicare.Messaging.Audiences.Internal.VanHanh.NhanSuActions nhanSu,
        AppDelicare.Messaging.Audiences.Internal.VanHanh.TraCuuActions traCuu, AppDelicare.Messaging.Audiences.Internal.VanHanh.ThaoTacActions thaoTac,
        PanelController panels, CommandScopeSync commands)
    {
        public async Task<DispatchResult> HandleAsync(BotInboundItemModel item, CancellationToken ct)
        {
            string bot = item.BotCode ?? string.Empty;
            var e = TelegramUpdateParser.Parse(TelegramUpdateParser.Deserialize(item.PayloadJson));
            if (e == null)
            {
                return DispatchResult.Ignored;
            }
            if (e.Kind == IncomingKind.ChatMember)
            {
                // kicked = người dùng chặn bot; member = mở lại.
                bool? blocked = e.MemberStatus == "kicked" ? true : e.MemberStatus == "member" ? false : null;
                if (blocked != null)
                {
                    await api.TouchIdentityAsync(new BotTouchRequest { BotCode = bot, ExternalUserId = e.FromId, Blocked = blocked }, ct);
                    users.Invalidate(bot, e.FromId);
                }
                return DispatchResult.Processed;
            }
            bool fromGroup = false;
            if (e.ChatType is not (null or "private"))
            {
                fromGroup = true;
                // BOT-1.7d: nhóm chat chỉ nhận tin thông báo; bấm nút trên tin trong nhóm → sự việc ghi ngay (thông báo nhỏ), nút khác mở trong chat riêng của người bấm.
                // BOT-3.1: ngoại lệ /chatid – quản trị gõ trong nhóm để lấy mã nhóm cấu hình nhận thông báo (người khác: bỏ qua, không lộ mã).
                if (e.Kind == IncomingKind.Command && e.Command == "chatid")
                {
                    var admin = await users.GetAsync(bot, e.FromId, ct);
                    if (admin.Linked && admin.CanUseBot && admin.IsAdmin)
                    {
                        await BotReply.TextAsync(sender, bot, e.ChatId, $"🆔 Mã nhóm này: {e.ChatId}", ct: ct);
                        return DispatchResult.Processed;
                    }
                    return DispatchResult.Ignored;
                }
                if (e.Kind != IncomingKind.Callback || e.CallbackId == null)
                {
                    return DispatchResult.Ignored;
                }
                var member = await users.GetAsync(bot, e.FromId, ct);
                if (!member.Linked || !member.CanUseBot)
                {
                    await sender.AnswerCallbackAsync(bot, e.CallbackId, "Bạn chưa liên kết bot – mở chat riêng với bot để liên kết.", ct);
                    return DispatchResult.Processed;
                }
                if (CallbackData.Parse(e.CallbackData) is { Kind: IncidentActions.Kind } incident)
                {
                    await incidents.GroupAsync(new BotActionContext(bot, e, member), incident, ct);
                    return DispatchResult.Processed;
                }
                await sender.AnswerCallbackAsync(bot, e.CallbackId, "Đã mở trong chat riêng với bot.", ct);
                e = e with { ChatId = e.FromId, ChatType = "private", MessageId = null, CallbackId = null };
            }
            // BOT-UX-1: chỉ chat riêng có bảng điều khiển; nhóm chat (và nút bấm từ nhóm) giữ hành vi cũ.
            bool panelChat = panels.Enabled && !fromGroup;
            bool stale = false;
            if (e.Kind == IncomingKind.Callback && e.CallbackId != null && panelChat && panels.IsStale(bot, e))
            {
                stale = true;
                await panels.RetireStaleAsync(bot, e, ct);
            }
            // BOT-UX-2 (mục 8): mọi nút bấm đều được trả lời bằng chữ ngắn (answerCallbackQuery) SAU khi xử lý – "Đã ghi 78 °C", "Đã mở: ME-0123", lỗi → câu lỗi api.
            var answer = e.Kind == IncomingKind.Callback && e.CallbackId != null && !stale ? CallbackAnswer.Begin() : null;
            bool failed = false;
            try
            {
                return await ProcessAsync(bot, e, panelChat, stale, ct);
            }
            catch (Exception)
            {
                failed = true;
                throw;
            }
            finally
            {
                if (answer != null)
                {
                    string text = failed ? "⚠️ Có lỗi – vui lòng thử lại." : answer.Resolve();
                    try
                    {
                        await sender.AnswerCallbackAsync(bot, e.CallbackId!, text, ct);
                    }
                    catch (Exception) when (failed)
                    {
                        // giữ lỗi gốc
                    }
                }
            }
        }

        private async Task<DispatchResult> ProcessAsync(string bot, IncomingEvent e, bool panelChat, bool stale, CancellationToken ct)
        {
            switch (e.Kind)
            {
                case IncomingKind.Contact:
                    await onboarding.ContactAsync(bot, e, ct);
                    return DispatchResult.Processed;
                case IncomingKind.Command when e.Command == "start":
                    await onboarding.StartAsync(bot, e, ct);
                    if (panelChat)
                    {
                        panels.KeyboardSent(bot, e.ChatId); // lời chào đã mang bàn phím dưới 3 phím
                    }
                    return DispatchResult.Processed;
                case IncomingKind.Command when e.Command == "huylienket":
                    await onboarding.UnlinkAsync(bot, e, ct);
                    return DispatchResult.Processed;
                case IncomingKind.Text when OnboardingActions.LooksLikeLinkCode(e.Text):
                    await onboarding.StartAsync(bot, e with { Argument = e.Text }, ct);
                    return DispatchResult.Processed;
            }
            var identity = await users.GetAsync(bot, e.FromId, ct);
            if (!identity.Linked)
            {
                await onboarding.AskPhoneAsync(bot, e, ct);
                CallbackAnswer.Set("Bạn chưa liên kết bot");
                return DispatchResult.Processed;
            }
            if (!identity.CanUseBot)
            {
                await BotReply.TextAsync(sender, bot, e.ChatId, "Tài khoản chưa được cấp quyền dùng bot này – liên hệ quản trị hệ thống.", KeyboardBuilder.Remove(), ct);
                return DispatchResult.Processed;
            }
            var ctx = new BotActionContext(bot, e, identity);
            // Trả lời câu hỏi ForceReply (vd. lý do từ chối duyệt). BOT-3.3: trả lời tin hỏi bằng FILE (vd. Excel máy chấm công) cũng là trả lời.
            var pending = e.Kind is IncomingKind.Text or IncomingKind.Document ? replies.Take(bot, e.ChatId, e.ReplyToMessageId?.ToString()) : null;
            var mode = panelChat ? PanelModeOf(bot, e, pending, stale) : null;
            if (mode == null)
            {
                return await RouteAsync(ctx, pending, ct);
            }
            var result = DispatchResult.Processed;
            await panels.RunAsync(bot, e.ChatId, mode.Value, async session =>
            {
                await commands.SyncAsync(bot, e.ChatId, identity, ct);
                // BOT-UX-2 (mục 4): bàn phím dưới 3 phím – gửi lại khi /menu, /ca, /help và lần tương tác đầu của chat sau khi container khởi động.
                await panels.EnsureKeyboardAsync(bot, e.ChatId, e.Kind == IncomingKind.Command && e.Command is "menu" or "ca" or "help", ct);
                if (pending != null)
                {
                    // Mục 6: nhận trả lời xong → xóa câu hỏi + câu trả lời, chức năng cập nhật panel.
                    await panels.AnsweredAsync(bot, e, ct);
                }
                else if (e.Kind is IncomingKind.Command || (e.Kind == IncomingKind.Text && IsKeyboardText(bot, identity, e.Text)))
                {
                    // Mục 4: lệnh / phím bàn phím dưới → xóa tin của người dùng, panel mới ở cuối chat.
                    await panels.DeleteAsync(bot, e.ChatId, e.MessageId, ct);
                }
                if (stale)
                {
                    // Mục 5: nút trên panel cũ → panel mới ở màn đầu.
                    await menu.OpenAsync(ctx, ct);
                    return;
                }
                var cb = e.Kind == IncomingKind.Callback ? CallbackData.Parse(e.CallbackData) : null;
                session.Callback = cb == null ? null : e.CallbackData;
                if (cb?.Kind == ShiftCards.Kind && registry.ByCode(cb.Id) is { } run && run.IsAllowed(bot, identity))
                {
                    panels.EnterAction(session.State, ctx, run);
                }
                result = await RouteAsync(ctx, pending, ct);
                if (cb?.Kind != CallbackData.Kinds.Nav)
                {
                    // BOT-UX-2 (mục 2): ghi màn vừa mở vào ngăn xếp (◀ Quay lại mở lại đúng màn trước). Nút điều hướng tự ghi khi phát lại màn.
                    PanelController.Commit(session);
                }
            }, ct);
            return result;
        }

        /// <summary>BOT-UX-1: cách đưa kết quả lên panel – null = không dùng panel (vị trí, liên hệ, ảnh, nút trên thẻ việc / tin hệ thống).</summary>
        private PanelMode? PanelModeOf(string bot, IncomingEvent e, PendingReply? pending, bool stale)
        {
            if (stale)
            {
                return PanelMode.NewAtBottom;
            }
            if (e.Kind == IncomingKind.Callback)
            {
                if (panels.IsCurrentPanel(bot, e))
                {
                    return PanelMode.EditCurrent;
                }
                // Nút chạy chức năng trên Thẻ ca (tin ghim – không sửa) → kết quả lên panel mới ở cuối.
                return CallbackData.Parse(e.CallbackData) is { Kind: ShiftCards.Kind, Action: "run" } ? PanelMode.NewAtBottom : null;
            }
            if (pending != null)
            {
                return PanelMode.EditCurrent;
            }
            return e.Kind is IncomingKind.Command or IncomingKind.Text ? PanelMode.NewAtBottom : null;
        }

        private bool IsKeyboardText(string bot, BotIdentityModel identity, string? text)
            => text is KeyboardBuilder.MyShift or KeyboardBuilder.Functions or KeyboardBuilder.CheckIn || registry.ByLabel(bot, identity, text) != null;

        private async Task<DispatchResult> RouteAsync(BotActionContext ctx, PendingReply? pending, CancellationToken ct)
        {
            string bot = ctx.Bot;
            var e = ctx.Event;
            var identity = ctx.Identity;
            if (pending != null)
            {
                switch (pending.Flow)
                {
                    case ApprovalActions.RejectFlow:
                        await approvals.ReplyAsync(ctx, pending, ct);
                        break;
                    case IncidentActions.ReplyNote:
                        await incidents.ReplyAsync(ctx, pending, ct);
                        break;
                    case AppDelicare.Messaging.Conversations.Flows.LeaveRequestFlow.ReplyDate or AppDelicare.Messaging.Conversations.Flows.LeaveRequestFlow.ReplyDays
                        or AppDelicare.Messaging.Conversations.Flows.LeaveRequestFlow.ReplyReason:
                        await leave.ReplyAsync(ctx, pending, ct);
                        break;
                    case AppDelicare.Messaging.Conversations.Flows.ShiftSwapFlow.ReplyReason:
                        await swap.ReplyAsync(ctx, pending, ct);
                        break;
                    case AppDelicare.Messaging.Audiences.Internal.VanHanh.BepActions.ReplyTemp or AppDelicare.Messaging.Audiences.Internal.VanHanh.BepActions.ReplyQty
                        or AppDelicare.Messaging.Audiences.Internal.VanHanh.BepActions.ReplyCorrective or AppDelicare.Messaging.Audiences.Internal.VanHanh.BepActions.ReplyExtraQty
                        or AppDelicare.Messaging.Audiences.Internal.VanHanh.BepActions.ReplyExtraReason or AppDelicare.Messaging.Audiences.Internal.VanHanh.BepActions.ReplyWaste or AppDelicare.Messaging.Audiences.Internal.VanHanh.BepActions.ReplyFinishFix:
                        await bep.ReplyAsync(ctx, pending, ct);
                        break;
                    case AppDelicare.Messaging.Audiences.Internal.VanHanh.GiaoVanActions.ReplyDispatchTemp or AppDelicare.Messaging.Audiences.Internal.VanHanh.GiaoVanActions.ReplyQty
                        or AppDelicare.Messaging.Audiences.Internal.VanHanh.GiaoVanActions.ReplyReceiver or AppDelicare.Messaging.Audiences.Internal.VanHanh.GiaoVanActions.ReplyBoxes
                        or AppDelicare.Messaging.Audiences.Internal.VanHanh.GiaoVanActions.ReplyDriver or AppDelicare.Messaging.Audiences.Internal.VanHanh.GiaoVanActions.ReplyReceiverName
                        or AppDelicare.Messaging.Audiences.Internal.VanHanh.GiaoVanActions.ReplyFail:
                        await giaoVan.ReplyAsync(ctx, pending, ct);
                        break;
                    case AppDelicare.Messaging.Audiences.Internal.VanHanh.DiemPhatActions.ReplyServed or AppDelicare.Messaging.Audiences.Internal.VanHanh.DiemPhatActions.ReplyReceive:
                        await diemPhat.ReplyAsync(ctx, pending, ct);
                        break;
                    case AppDelicare.Messaging.Audiences.Internal.VanHanh.PhanAnhActions.ReplyCustomer or AppDelicare.Messaging.Audiences.Internal.VanHanh.PhanAnhActions.ReplyDescription
                        or AppDelicare.Messaging.Audiences.Internal.VanHanh.PhanAnhActions.ReplyResolve or AppDelicare.Messaging.Audiences.Internal.VanHanh.PhanAnhActions.ReplyComment:
                        await phanAnh.ReplyAsync(ctx, pending, ct);
                        break;
                    case AppDelicare.Messaging.Audiences.Internal.VanHanh.NhanSuActions.ReplyProxy:
                        await nhanSu.ReplyAsync(ctx, pending, ct);
                        break;
                    case AppDelicare.Messaging.Audiences.Internal.VanHanh.TraCuuActions.ReplyTrace:
                        await traCuu.ReplyAsync(ctx, pending, ct);
                        break;
                    case AppDelicare.Messaging.Audiences.Internal.VanHanh.ThaoTacActions.ReplyDishFrom or AppDelicare.Messaging.Audiences.Internal.VanHanh.ThaoTacActions.ReplyDishTo
                        or AppDelicare.Messaging.Audiences.Internal.VanHanh.ThaoTacActions.ReplyChangeReason or AppDelicare.Messaging.Audiences.Internal.VanHanh.ThaoTacActions.ReplyCleanReject
                        or AppDelicare.Messaging.Audiences.Internal.VanHanh.ThaoTacActions.ReplyAttendanceFile:
                        await thaoTac.ReplyAsync(ctx, pending, ct);
                        break;
                }
                return DispatchResult.Processed;
            }
            if (e.Kind == IncomingKind.Callback)
            {
                var data = CallbackData.Parse(e.CallbackData);
                switch (data?.Kind)
                {
                    case CallbackData.Kinds.Menu:
                        await menu.HandleAsync(ctx, data, ct);
                        break;
                    case CallbackData.Kinds.Nav:
                        await panels.NavAsync(ctx, data, menu, async (c, t) => await RouteAsync(c, null, t), ct);
                        break;
                    case CallbackData.Kinds.Page:
                        await panels.PageAsync(ctx, data, ct);
                        break;
                    case ShiftCards.Kind:
                        await shiftCards.HandleAsync(ctx, data, ct);
                        break;
                    case IncidentActions.Kind:
                        await incidents.HandleAsync(ctx, data, ct);
                        break;
                    case CallbackData.Kinds.Approval:
                        await approvals.HandleAsync(ctx, data, ct);
                        break;
                    case AppDelicare.Messaging.Conversations.Flows.LeaveRequestFlow.Kind:
                        await leave.HandleAsync(ctx, data, ct);
                        break;
                    case AppDelicare.Messaging.Conversations.Flows.ShiftSwapFlow.Kind:
                        await swap.HandleAsync(ctx, data, ct);
                        break;
                    case CallbackData.Kinds.Batch:
                        await bep.HandleAsync(ctx, data, ct);
                        break;
                    case CallbackData.Kinds.Checklist:
                        await attp.HandleAsync(ctx, data, ct);
                        break;
                    case CallbackData.Kinds.Delivery:
                        await giaoVan.HandleAsync(ctx, data, ct);
                        break;
                    case CallbackData.Kinds.Reconciliation:
                        await diemPhat.HandleAsync(ctx, data, ct);
                        break;
                    case CallbackData.Kinds.Complaint:
                        await phanAnh.HandleAsync(ctx, data, ct);
                        break;
                    case CallbackData.Kinds.Hr:
                        await nhanSu.HandleAsync(ctx, data, ct);
                        break;
                    case CallbackData.Kinds.Op:
                        await thaoTac.HandleAsync(ctx, data, ct);
                        break;
                    case CallbackData.Kinds.Undo when data.Action == "apv":
                        await approvals.UndoAsync(ctx, data, ct);
                        break;
                    default:
                        await BotReply.TextAsync(sender, bot, e.ChatId, "Nút đã hết hạn.", ct: ct);
                        break;
                }
                return DispatchResult.Processed;
            }
            if (e.Kind == IncomingKind.Location && (e.Location?.LivePeriod > 0 || e.Edited))
            {
                // BOT-2.4: vị trí trực tiếp (tin vị trí có live_period / cập nhật bằng edited_message) → giao vận; vị trí gửi 1 lần → chấm công như cũ.
                await giaoVan.LiveLocationAsync(ctx, ct);
                return DispatchResult.Processed;
            }
            if (e.Kind == IncomingKind.Location)
            {
                await self.LocationAsync(ctx, ct);
                return DispatchResult.Processed;
            }
            var action = e.Kind == IncomingKind.Command ? registry.ByCommand(bot, identity, e.Command) : registry.ByLabel(bot, identity, e.Text);
            if (action != null)
            {
                if (PanelSession.Current is { } s)
                {
                    panels.EnterAction(s.State, ctx, action);
                }
                await action.Handler(ctx, ct);
                return DispatchResult.Processed;
            }
            if ((e.Kind == IncomingKind.Command && e.Command == "menu") || e.Text == KeyboardBuilder.Functions)
            {
                await menu.OpenAsync(ctx, ct);
                return DispatchResult.Processed;
            }
            if (e.Kind == IncomingKind.Text && e.Text == ContextKeyboard.Exit)
            {
                await BotReply.TextAsync(sender, bot, e.ChatId, "Đã thoát.", KeyboardBuilder.MainMenu(), ct);
                return DispatchResult.Processed;
            }
            if (e.Kind == IncomingKind.Text && (search.LooksLikeCode(e.Text) || CodeSearch.LooksLikeSearch(e.Text)))
            {
                if (PanelSession.Current is { } ls)
                {
                    PanelController.EnterLoose(ls.State, "🔎 Tra cứu");
                }
                await search.SearchAsync(ctx, e.Text!, ct);
                return DispatchResult.Processed;
            }
            if (PanelSession.Current is { } hs)
            {
                PanelController.EnterLoose(hs.State, "❓ Trợ giúp");
            }
            await help.HelpAsync(bot, e, ct);
            return DispatchResult.Processed;
        }
    }
}
