using AppDelicare.Messaging.Audiences.Internal.Chung;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Channels.Telegram;
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
    /// BOT-1.3 (khung): update → hành động. Lệnh / nút / liên hệ / gõ mã → Onboarding / Help; my_chat_member → chặn / mở bot; nhóm chat → bỏ qua (BOT-2.x).
    /// BOT-1.4 thêm: menu theo quyền, nút trong tin (CallbackData), ForceReply, vị trí, ảnh.
    /// </summary>
    public sealed class UpdateDispatcher(OnboardingActions onboarding, HelpActions help, BotUserContext users, IBotChannelApi api, IChannelSender sender)
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
                return DispatchResult.Ignored; // nhóm chat: BOT-2.x
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
            if (e.Kind == IncomingKind.Command && e.Command is "help" or "menu" || e.Text == KeyboardBuilder.Help)
            {
                await help.HelpAsync(bot, e, ct);
            }
            else if (e.Text == KeyboardBuilder.MyShift)
            {
                await help.MyShiftAsync(bot, e, identity, ct);
            }
            else if (e.Text == KeyboardBuilder.Functions || e.Kind == IncomingKind.Callback)
            {
                await help.FunctionsAsync(bot, e, ct);
            }
            else
            {
                await help.HelpAsync(bot, e, ct);
            }
            return DispatchResult.Processed;
        }
    }
}
