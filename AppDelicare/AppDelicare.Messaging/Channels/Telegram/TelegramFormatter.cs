using System.Text;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Presentation;

namespace AppDelicare.Messaging.Channels.Telegram
{
    /// <summary>
    /// BOT-1.3: nội dung trung tính → HTML Telegram (escape &amp; &lt; &gt;, tiêu đề đậm, cắt 3.800 ký tự) + bàn phím nút trong tin.
    /// BOT-UX-2: dòng kết quả (<see cref="NeutralMessage.Banner"/>) đứng trên tiêu đề; nút cùng nhóm hàng (<see cref="NeutralButton.Row"/>) nằm chung 1 hàng.
    /// </summary>
    public static class TelegramFormatter
    {
        public const int MaxLength = 3800;

        /// <summary>Telegram HTML chỉ cần thoát &amp; &lt; &gt; – giữ nguyên chữ Việt / emoji (WebUtility.HtmlEncode đổi thành &amp;#…; làm tin dài gấp nhiều lần).</summary>
        public static string Escape(string? s) => (s ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        public static (string Html, TgInlineKeyboard? Keyboard) Render(NeutralMessage message)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(message.Banner))
            {
                sb.Append(Escape(message.Banner));
            }
            if (!string.IsNullOrWhiteSpace(message.Title))
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }
                sb.Append(LevelIcon(message.Level)).Append("<b>").Append(Escape(message.Title)).Append("</b>");
            }
            foreach (var line in message.Lines)
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }
                sb.Append(Escape(line));
            }
            string html = sb.ToString();
            if (html.Length > MaxLength)
            {
                // Cắt ở xuống dòng gần nhất để không cắt giữa thẻ / thực thể HTML.
                int cut = html.LastIndexOf('\n', MaxLength - 1);
                html = html[..(cut > MaxLength / 2 ? cut : MaxLength - 1)] + "\n…";
            }
            TgInlineKeyboard? keyboard = null;
            if (message.Buttons.Count > 0)
            {
                keyboard = new TgInlineKeyboard { InlineKeyboard = Rows(message.Buttons) };
            }
            return (html, keyboard);
        }

        /// <summary>BOT-UX-2: nút liền nhau cùng nhóm hàng → 1 hàng (≤ <see cref="NeutralMessage.MaxRowWidth"/> nút); nút không nhóm → 2 nút / hàng như cũ.</summary>
        public static List<List<TgInlineButton>> Rows(IReadOnlyList<NeutralButton> buttons)
        {
            var rows = new List<List<TgInlineButton>>();
            int i = 0;
            while (i < buttons.Count)
            {
                string? group = buttons[i].Row;
                int width = group == null ? 2 : NeutralMessage.MaxRowWidth;
                var row = new List<TgInlineButton>();
                while (i < buttons.Count && buttons[i].Row == group && row.Count < width)
                {
                    var b = buttons[i++];
                    row.Add(new TgInlineButton { Text = b.Text, CallbackData = b.Url == null ? b.Callback : null, Url = b.Url });
                }
                rows.Add(row);
            }
            return rows;
        }

        private static string LevelIcon(string? level) => level switch
        {
            "URGENT" => "🔴 ",
            _ => string.Empty,
        };
    }
}
