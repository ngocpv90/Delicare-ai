using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Channels.Telegram;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using AppDelicare.Messaging.Experience;
using AppDelicare.Messaging.Options;
using AppDelicare.Messaging.Presentation;
using Microsoft.Extensions.Options;

namespace AppDelicare.Messaging.Audiences.Internal.Chung
{
    /// <summary>
    /// BOT-1.3 (BOT_TELEGRAM §2): đăng ký / liên kết. <c>/start</c> (không mã, <c>CHAMCONG</c> từ QR cổng) → chưa liên kết thì xin chia sẻ số điện thoại;
    /// <c>/start &lt;mã&gt;</c> (mã web / HR) → LinkByCode; liên hệ chia sẻ → chỉ nhận số của chính người gửi → LinkByPhone; <c>/huylienket</c> → gỡ.
    /// Bot không tự quyết: kết quả và câu báo lỗi lấy từ api.
    /// </summary>
    public sealed class OnboardingActions(IBotChannelApi api, BotUserContext users, UserTokenCache tokens, IChannelSender sender, IOptionsMonitor<TelegramOptions> telegram)
    {
        /// <summary>Tham số /start không phải mã liên kết (QR cổng chấm công; QR vật thể ở BOT-2.4).</summary>
        private static readonly HashSet<string> StartKeywords = new(StringComparer.OrdinalIgnoreCase) { "CHAMCONG" };

        public static bool LooksLikeLinkCode(string? s)
            => s != null && s.Length is >= 40 and <= 64 && s.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

        public async Task StartAsync(string bot, IncomingEvent e, CancellationToken ct)
        {
            string? arg = e.Argument?.Trim();
            if (!string.IsNullOrEmpty(arg) && !StartKeywords.Contains(arg))
            {
                await LinkByCodeAsync(bot, e, arg, ct);
                return;
            }
            var touch = await api.TouchIdentityAsync(new BotTouchRequest { BotCode = bot, ExternalUserId = e.FromId, ExternalUsername = e.Username, DisplayName = e.DisplayName, Blocked = false }, ct);
            var identity = touch.Data is { Linked: true } ? touch.Data : await users.GetAsync(bot, e.FromId, ct);
            if (identity.Linked)
            {
                users.Set(bot, e.FromId, identity);
                await WelcomeAsync(bot, e, identity, ct);
                return;
            }
            await AskPhoneAsync(bot, e, ct);
        }

        public Task AskPhoneAsync(string bot, IncomingEvent e, CancellationToken ct)
            => BotReply.SendAsync(sender, bot, e.ChatId, new NeutralMessage
            {
                Title = "Chào mừng đến với Delicare",
                Lines =
                [
                    "Bấm nút \"" + KeyboardBuilder.SharePhone + "\" bên dưới để liên kết tài khoản.",
                    "Nếu bạn có mã liên kết lấy trên web (hoặc phòng Nhân sự cấp), gửi: /start <mã>",
                ],
            }, KeyboardBuilder.Phone(), ct);

        public async Task ContactAsync(string bot, IncomingEvent e, CancellationToken ct)
        {
            // Telegram xác thực số thuộc tài khoản khi contact.user_id = người gửi; danh bạ người khác → từ chối (api kiểm lại).
            if (e.Contact?.UserId?.ToString() != e.FromId)
            {
                await BotReply.TextAsync(sender, bot, e.ChatId, "Vui lòng bấm nút \"" + KeyboardBuilder.SharePhone + "\" để chia sẻ số điện thoại của chính bạn.", KeyboardBuilder.Phone(), ct);
                return;
            }
            var r = await api.LinkByPhoneAsync(new BotLinkByPhoneRequest
            {
                BotCode = bot, ExternalUserId = e.FromId, ContactUserId = e.Contact.UserId?.ToString(), Phone = e.Contact.PhoneNumber,
                ExternalUsername = e.Username, DisplayName = e.DisplayName,
            }, ct);
            await AfterLinkAsync(bot, e, r, ct);
        }

        public async Task UnlinkAsync(string bot, IncomingEvent e, CancellationToken ct)
        {
            var r = await api.UnlinkIdentityAsync(new BotIdentityRequest { BotCode = bot, ExternalUserId = e.FromId }, ct);
            users.Invalidate(bot, e.FromId);
            tokens.Invalidate(bot, e.FromId);
            await BotReply.TextAsync(sender, bot, e.ChatId,
                r.IsOk ? "Đã hủy liên kết tài khoản. Gửi /start để liên kết lại." : r.ResponseMessage ?? "Không hủy được liên kết.", KeyboardBuilder.Remove(), ct);
        }

        private async Task LinkByCodeAsync(string bot, IncomingEvent e, string code, CancellationToken ct)
        {
            var r = await api.LinkByCodeAsync(new BotLinkByCodeRequest
            {
                BotCode = bot, ExternalUserId = e.FromId, Code = code, ExternalUsername = e.Username, DisplayName = e.DisplayName,
            }, ct);
            await AfterLinkAsync(bot, e, r, ct);
        }

        private async Task AfterLinkAsync(string bot, IncomingEvent e, ApiResult<BotIdentityModel> r, CancellationToken ct)
        {
            users.Invalidate(bot, e.FromId);
            tokens.Invalidate(bot, e.FromId);
            if (!r.IsOk || r.Data is not { Linked: true } identity)
            {
                await BotReply.TextAsync(sender, bot, e.ChatId, r.ResponseMessage ?? "Không liên kết được – vui lòng liên hệ phòng Nhân sự.", KeyboardBuilder.Remove(), ct);
                return;
            }
            users.Set(bot, e.FromId, identity);
            await WelcomeAsync(bot, e, identity, ct);
        }

        private async Task WelcomeAsync(string bot, IncomingEvent e, BotIdentityModel identity, CancellationToken ct)
        {
            var lines = new List<string>();
            if (!identity.CanUseBot)
            {
                lines.Add("Tài khoản chưa được cấp quyền dùng bot này – liên hệ quản trị hệ thống.");
            }
            else
            {
                lines.Add("Dùng 3 nút bên dưới: " + KeyboardBuilder.MyShift + " · " + KeyboardBuilder.Functions + " · " + KeyboardBuilder.Help + ".");
            }
            if (identity.NeedsCode)
            {
                lines.Add("Bạn có quyền duyệt: để duyệt trên bot, lấy mã liên kết trên web (Hồ sơ cá nhân → Liên kết Telegram) rồi gửi /start <mã>.");
            }
            var message = new NeutralMessage { Title = "Xin chào " + (identity.FullName ?? e.DisplayName ?? "bạn"), Lines = lines };
            await BotReply.SendAsync(sender, bot, e.ChatId, message, identity.CanUseBot ? KeyboardBuilder.MainMenu() : KeyboardBuilder.Remove(), ct);
            // Người có quyền bot Điều hành: bot VH gửi nút mở bot DH (Telegram bắt buộc /start bot DH một lần – BOT_TELEGRAM §2.4).
            string? dh = telegram.CurrentValue.Bots.TryGetValue("DH", out var d) ? d.Username : null;
            if (identity.CanUseDieuHanh && string.Equals(bot, "VH", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(dh))
            {
                await BotReply.SendAsync(sender, bot, e.ChatId, new NeutralMessage
                {
                    Lines = ["Bạn dùng được bot Điều hành (việc liên phòng, duyệt, bản tin)."],
                    Buttons = [new NeutralButton("Mở bot Điều hành", Url: $"https://t.me/{dh.TrimStart('@')}?start=CHAMCONG")],
                }, ct: ct);
            }
        }
    }
}
