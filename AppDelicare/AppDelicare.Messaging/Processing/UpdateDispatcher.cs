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
        AppDelicare.Messaging.Audiences.Internal.VanHanh.TraCuuActions traCuu, AppDelicare.Messaging.Audiences.Internal.VanHanh.ThaoTacActions thaoTac)
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
            if (e.ChatType is not (null or "private"))
            {
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
            if (e.Kind == IncomingKind.Callback && e.CallbackId != null)
            {
                await sender.AnswerCallbackAsync(bot, e.CallbackId, null, ct);
            }
            switch (e.Kind)
            {
                case IncomingKind.Contact:
                    await onboarding.ContactAsync(bot, e, ct);
                    return DispatchResult.Processed;
                case IncomingKind.Command when e.Command == "start":
                    await onboarding.StartAsync(bot, e, ct);
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
                return DispatchResult.Processed;
            }
            if (!identity.CanUseBot)
            {
                await BotReply.TextAsync(sender, bot, e.ChatId, "Tài khoản chưa được cấp quyền dùng bot này – liên hệ quản trị hệ thống.", KeyboardBuilder.Remove(), ct);
                return DispatchResult.Processed;
            }
            var ctx = new BotActionContext(bot, e, identity);

            // Trả lời câu hỏi ForceReply (vd. lý do từ chối duyệt).
            // BOT-3.3: trả lời tin hỏi bằng FILE (vd. Excel máy chấm công) cũng là trả lời.
            if (e.Kind is IncomingKind.Text or IncomingKind.Document && replies.Take(bot, e.ChatId, e.ReplyToMessageId?.ToString()) is { } pending)
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
                        or AppDelicare.Messaging.Audiences.Internal.VanHanh.BepActions.ReplyExtraReason or AppDelicare.Messaging.Audiences.Internal.VanHanh.BepActions.ReplyWaste:
                        await bep.ReplyAsync(ctx, pending, ct);
                        break;
                    case AppDelicare.Messaging.Audiences.Internal.VanHanh.GiaoVanActions.ReplyDispatchTemp or AppDelicare.Messaging.Audiences.Internal.VanHanh.GiaoVanActions.ReplyQty
                        or AppDelicare.Messaging.Audiences.Internal.VanHanh.GiaoVanActions.ReplyReceiver or AppDelicare.Messaging.Audiences.Internal.VanHanh.GiaoVanActions.ReplyBoxes
                        or AppDelicare.Messaging.Audiences.Internal.VanHanh.GiaoVanActions.ReplyDriver:
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
                await BotReply.TextAsync(sender, bot, e.ChatId, "Đã thoát.", KeyboardBuilder.ForActions(registry.Available(bot, identity)), ct);
                return DispatchResult.Processed;
            }
            if (e.Kind == IncomingKind.Text && (search.LooksLikeCode(e.Text) || CodeSearch.LooksLikeSearch(e.Text)))
            {
                await search.SearchAsync(ctx, e.Text!, ct);
                return DispatchResult.Processed;
            }
            await help.HelpAsync(bot, e, ct);
            return DispatchResult.Processed;
        }
    }
}
