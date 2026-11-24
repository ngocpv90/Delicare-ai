using System.Text;
using AppDelicare.Messaging.Callback;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Channels.Telegram.Models;
using Microsoft.Extensions.Options;

namespace AppDelicare.Messaging.Experience
{
    /// <summary>
    /// BOT-UX-1 (mục 2, 3, 6, 7): bọc <see cref="IChannelSender"/>. Ngoài phiên panel (<see cref="PanelSession.Current"/> = null – tin hệ thống, nhóm chat, panel tắt) → chuyển thẳng.
    /// Trong phiên: tin chức năng gửi ra (chữ + nút inline) → SỬA panel (đầu panel có đường dẫn "Menu › … ", cuối có [◀ Quay lại] [🏠 Menu] [✖ Đóng]);
    /// sửa chính panel → thay nội dung; tin hỏi ForceReply / bàn phím dưới → gửi riêng (ForceReply ghi là nháp); sửa tin khác (thẻ việc, thẻ ca) → chuyển thẳng.
    /// Nội dung bắt đầu bằng ✅ và không có nút → phiếu kết quả (giờ + [▶ Tiếp tục] [🏠 Menu]).
    /// </summary>
    public sealed class PanelSender(IChannelSender inner, IOptionsMonitor<PanelOptions> options, TimeProvider time) : IChannelSender
    {
        public const int MaxHtml = 3900;
        public const string Separator = " › ";

        public IChannelSender Inner => inner;

        public async Task<ChannelSendResult> SendAsync(string botCode, OutgoingMessage message, CancellationToken ct = default)
        {
            // BOT-UX-2 (mục 8): nội dung vừa gửi → chữ trả lời nút bấm (dòng ✅ / ⚠️ hoặc tiêu đề màn).
            CallbackAnswer.Offer(message.Text);
            var s = Session(botCode, message.ChatId);
            if (s == null)
            {
                return await inner.SendAsync(botCode, message, ct);
            }
            switch (message.ReplyMarkup)
            {
                case TgForceReply:
                {
                    // Chức năng mở ra chỉ để hỏi gõ tay → panel hiện bước đang nhập (+ Quay lại / Menu / Đóng) rồi câu hỏi nằm ngay dưới.
                    if (s.Parts.Count == 0 && !s.Rendered)
                    {
                        s.Parts.Add(("✏️ " + message.Text, []));
                        await RenderAsync(s, ct);
                    }
                    var sent = await inner.SendAsync(botCode, message, ct);
                    if (sent.ProviderMessageId != null)
                    {
                        s.State.DraftQuestions.Add(sent.ProviderMessageId);
                    }
                    return sent;
                }
                case null:
                case TgInlineKeyboard:
                    s.Parts.Add((message.Text, Rows(message.ReplyMarkup)));
                    return await RenderAsync(s, ct);
                default:
                    // Bàn phím dưới (reply keyboard / remove) không gắn được vào tin có nút inline → gửi riêng như cũ.
                    return await inner.SendAsync(botCode, message, ct);
            }
        }

        public async Task<ChannelSendResult> EditAsync(string botCode, string chatId, string providerMessageId, string text, object? replyMarkup, CancellationToken ct = default)
        {
            CallbackAnswer.Offer(text);
            var s = Session(botCode, chatId);
            if (s == null || providerMessageId != s.State.MessageId || replyMarkup is not (null or TgInlineKeyboard))
            {
                return await inner.EditAsync(botCode, chatId, providerMessageId, text, replyMarkup, ct);
            }
            s.Parts.Clear();
            s.Parts.Add((text, Rows(replyMarkup)));
            return await RenderAsync(s, ct);
        }

        public Task<ChannelSendResult> PinAsync(string botCode, string chatId, string providerMessageId, CancellationToken ct = default)
            => inner.PinAsync(botCode, chatId, providerMessageId, ct);

        public Task AnswerCallbackAsync(string botCode, string callbackId, string? text = null, CancellationToken ct = default)
            => inner.AnswerCallbackAsync(botCode, callbackId, text, ct);

        public Task<byte[]?> DownloadFileAsync(string botCode, string fileId, CancellationToken ct = default)
            => inner.DownloadFileAsync(botCode, fileId, ct);

        public Task<ChannelSendResult> DeleteAsync(string botCode, string chatId, string providerMessageId, CancellationToken ct = default)
            => inner.DeleteAsync(botCode, chatId, providerMessageId, ct);

        private static PanelSession? Session(string bot, string chatId)
            => PanelSession.Current is { } s && s.Bot == bot && s.ChatId == chatId ? s : null;

        private static List<List<TgInlineButton>> Rows(object? markup)
            => markup is TgInlineKeyboard kb ? [.. kb.InlineKeyboard.Select(r => r.ToList())] : [];

        /// <summary>Vẽ panel từ nội dung đã gom: mới ở cuối chat (xóa panel cũ) hoặc sửa panel hiện tại.</summary>
        public async Task<ChannelSendResult> RenderAsync(PanelSession s, CancellationToken ct)
        {
            var (html, keyboard) = Compose(s);
            var state = s.State;
            if (s.Mode == PanelMode.EditCurrent || s.Rendered)
            {
                if (state.MessageId != null)
                {
                    var edit = await inner.EditAsync(s.Bot, s.ChatId, state.MessageId, html, keyboard, ct);
                    if (edit.Status == ChannelSendStatus.Sent || IsNotModified(edit))
                    {
                        s.Rendered = true;
                        return ChannelSendResult.Sent(state.MessageId);
                    }
                }
            }
            await CloseAsync(s.Bot, s.ChatId, state, ct);
            var sent = await inner.SendAsync(s.Bot, new OutgoingMessage(s.ChatId, html, keyboard), ct);
            if (sent.ProviderMessageId != null)
            {
                state.MessageId = sent.ProviderMessageId;
                state.CreatedUtc = time.GetUtcNow().UtcDateTime;
                s.Rendered = true;
            }
            return sent;
        }

        /// <summary>Bỏ panel hiện tại: xóa (trong 48 giờ) hoặc sửa thành "Menu đã đóng" + bỏ nút; ghi vào danh sách panel cũ.</summary>
        public async Task CloseAsync(string bot, string chatId, PanelState state, CancellationToken ct)
        {
            if (state.MessageId == null)
            {
                return;
            }
            string id = state.MessageId;
            state.MessageId = null;
            state.Retire(id);
            await RemoveAsync(bot, chatId, id, time.GetUtcNow().UtcDateTime - state.CreatedUtc, ct);
        }

        /// <summary>Xóa 1 tin của bot; quá hạn xóa / lỗi → sửa "Menu đã đóng" + bỏ nút.</summary>
        public async Task RemoveAsync(string bot, string chatId, string messageId, TimeSpan? age, CancellationToken ct)
        {
            if (age == null || age.Value.TotalHours < options.CurrentValue.DeleteWithinHours)
            {
                var deleted = await inner.DeleteAsync(bot, chatId, messageId, ct);
                if (deleted.Status == ChannelSendStatus.Sent)
                {
                    return;
                }
            }
            await inner.EditAsync(bot, chatId, messageId, "Menu đã đóng.", new TgInlineKeyboard(), ct);
        }

        private static bool IsNotModified(ChannelSendResult r) => (r.Error ?? string.Empty).Contains("not modified", StringComparison.OrdinalIgnoreCase);

        public static bool IsResult(PanelSession s)
            => s.Parts.Count > 0 && s.Parts.All(p => p.Rows.Count == 0) && s.Parts[^1].Html.TrimStart().StartsWith('✅');

        private (string Html, TgInlineKeyboard Keyboard) Compose(PanelSession s)
        {
            // BOT-UX-2: đường dẫn + hàng điều hướng theo ngăn xếp SAU lần xử lý này (màn vừa mở đã có trên đường dẫn).
            var stack = PanelController.Plan(s).Stack;
            var sb = new StringBuilder();
            sb.Append("<i>").Append(AppDelicare.Messaging.Channels.Telegram.TelegramFormatter.Escape(Breadcrumb(stack))).Append("</i>");
            string body = string.Join("\n\n", s.Parts.Select(p => p.Html).Where(h => !string.IsNullOrWhiteSpace(h)));
            if (body.Length > 0)
            {
                sb.Append("\n\n").Append(body);
            }
            bool result = IsResult(s);
            if (result)
            {
                sb.Append("\n🕒 ").Append(time.GetLocalNow().ToString("HH:mm dd/MM"));
            }
            string html = sb.ToString();
            if (html.Length > MaxHtml)
            {
                int cut = html.LastIndexOf('\n', MaxHtml - 1);
                html = html[..(cut > MaxHtml / 2 ? cut : MaxHtml - 1)] + "\n… (còn nữa – xem đủ trên web)";
            }
            var rows = s.Parts.SelectMany(p => p.Rows).Where(r => r.Count > 0).Take(90).ToList();
            rows.Add(result ? ResultRow() : NavRow(stack));
            return (html, new TgInlineKeyboard { InlineKeyboard = rows });
        }

        /// <summary>BOT-UX-2: đường dẫn dài hơn số ký tự này (và &gt; 4 bước) → rút gọn giữa: "Menu › … › 2 bước cuối".</summary>
        public const int CrumbMax = 56;

        public static string Breadcrumb(PanelState state) => Breadcrumb(state.Stack);

        /// <summary>BOT-UX-2 (mục 3): đường dẫn đủ cấp (vd. Menu › 🍳 Bếp › 🍳 Mẻ cần làm › 🔥 ME-0123), dài thì rút gọn giữa bằng "…".</summary>
        public static string Breadcrumb(IReadOnlyList<PanelEntry> stack)
        {
            if (stack.Count == 0)
            {
                return "Menu";
            }
            var parts = new List<string>();
            foreach (var e in stack)
            {
                parts.Add(e.Level == PanelLevel.Root ? "Menu" : e.Label);
                if (e.Detail != null)
                {
                    parts.Add(e.Detail);
                }
            }
            string full = string.Join(Separator, parts);
            return full.Length <= CrumbMax || parts.Count <= 4 ? full : string.Join(Separator, [parts[0], "…", parts[^2], parts[^1]]);
        }

        public static List<TgInlineButton> NavRow(PanelState state) => NavRow(state.Stack);

        public static List<TgInlineButton> NavRow(IReadOnlyList<PanelEntry> stack)
        {
            var row = new List<TgInlineButton>();
            if (stack.Count > 0 && stack[^1].Level != PanelLevel.Root)
            {
                row.Add(Btn("◀ Quay lại", PanelNav.Back));
                row.Add(Btn("🏠 Menu", PanelNav.Home));
            }
            row.Add(Btn("✖ Đóng", PanelNav.Close));
            return row;
        }

        public static List<TgInlineButton> ResultRow() => [Btn("▶ Tiếp tục", PanelNav.Continue), Btn("🏠 Menu", PanelNav.Home)];

        private static TgInlineButton Btn(string text, string action) => new() { Text = text, CallbackData = CallbackData.Build(CallbackData.Kinds.Nav, action) };
    }

    /// <summary>Mã nút điều hướng panel (<c>nav:&lt;mã&gt;</c>).</summary>
    public static class PanelNav
    {
        public const string Back = "back";
        public const string Home = "home";
        public const string Close = "close";
        public const string Continue = "cont";
    }
}
