using System.Text;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Presentation;

namespace AppDelicare.Messaging.Channels.Telegram
{
    /// <summary>BOT-1.3: nội dung trung tính → HTML Telegram (escape &amp; &lt; &gt;, tiêu đề đậm, cắt 3.800 ký tự) + bàn phím nút trong tin.</summary>
    public static class TelegramFormatter
    {
        public const int MaxLength = 3800;

        /// <summary>Telegram HTML chỉ cần thoát &amp; &lt; &gt; – giữ nguyên chữ Việt / emoji (WebUtility.HtmlEncode đổi thành &amp;#…; làm tin dài gấp nhiều lần).</summary>
        public static string Escape(string? s) => (s ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        public static (string Html, TgInlineKeyboard? Keyboard) Render(NeutralMessage message)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(message.Title))
            {
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
                keyboard = new TgInlineKeyboard
                {
                    InlineKeyboard = message.Buttons.Chunk(2)
                        .Select(row => row.Select(b => new TgInlineButton { Text = b.Text, CallbackData = b.Url == null ? b.Callback : null, Url = b.Url }).ToList())
                        .ToList(),
                };
            }
            return (html, keyboard);
        }

        private static string LevelIcon(string? level) => level switch
        {
            "URGENT" => "🔴 ",
            _ => string.Empty,
        };
    }
}
