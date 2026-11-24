using System.Net;
using System.Text.RegularExpressions;

namespace AppDelicare.Messaging.Experience
{
    /// <summary>
    /// BOT-UX-2 (mục 8): chữ trả lời nút bấm (answerCallbackQuery – thông báo nhỏ trên màn hình) – KHÔNG để rỗng. Mở 1 lần cho mỗi lần bấm nút ở chat riêng
    /// (<see cref="Begin"/>, gắn vào luồng async), chức năng đặt chữ rõ (<see cref="Set"/>, vd. "Đã ghi 78 °C"); không đặt thì lấy từ nội dung vừa gửi
    /// (<see cref="Offer"/> – PanelSender gọi): dòng kết quả ✅ / ⚠️ / ❌ đầu tiên, không có thì tiêu đề màn vừa mở, cuối cùng "Đã nhận".
    /// </summary>
    public sealed class CallbackAnswer
    {
        public const int MaxLength = 64;
        private static readonly AsyncLocal<CallbackAnswer?> CurrentAnswer = new();
        private static readonly Regex Bold = new("<b>(.*?)</b>", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        private static readonly Regex Tag = new("<[^>]+>", RegexOptions.CultureInvariant);
        private static readonly string[] StatusMarks = ["✅", "⚠️", "❌", "↩️", "⏳", "⛔"];

        private string? _explicit;
        private string? _status;
        private string? _title;
        private string? _first;

        public static CallbackAnswer? Current => CurrentAnswer.Value is { Done: false } a ? a : null;

        public bool Done { get; private set; }

        public static CallbackAnswer Begin()
        {
            var a = new CallbackAnswer();
            CurrentAnswer.Value = a;
            return a;
        }

        /// <summary>Chức năng đặt chữ trả lời rõ ràng (ưu tiên hơn chữ suy ra từ nội dung).</summary>
        public static void Set(string text)
        {
            if (Current is { } a)
            {
                a._explicit = text;
            }
        }

        /// <summary>Nội dung (HTML Telegram) vừa gửi / sửa trong lần bấm này.</summary>
        public static void Offer(string? html)
        {
            if (Current is not { } a || string.IsNullOrWhiteSpace(html))
            {
                return;
            }
            var lines = html.Split('\n').Select(Plain).Where(l => l.Length > 0).ToList();
            a._status ??= lines.FirstOrDefault(l => StatusMarks.Any(m => l.StartsWith(m, StringComparison.Ordinal)));
            if (a._title == null && Bold.Match(html) is { Success: true } m)
            {
                a._title = Plain(m.Groups[1].Value);
            }
            a._first ??= lines.FirstOrDefault();
        }

        /// <summary>Chữ trả lời cuối cùng (≤ 64 ký tự, không rỗng) – đánh dấu đã trả lời.</summary>
        public string Resolve()
        {
            Done = true;
            if (CurrentAnswer.Value == this)
            {
                CurrentAnswer.Value = null;
            }
            string text = _explicit ?? _status ?? (_title != null ? "Đã mở: " + _title : null) ?? _first ?? "Đã nhận";
            return Short(text);
        }

        public static string Short(string text)
        {
            text = text.Trim();
            return text.Length <= MaxLength ? text : text[..(MaxLength - 1)].TrimEnd() + "…";
        }

        private static string Plain(string s) => WebUtility.HtmlDecode(Tag.Replace(s, string.Empty)).Trim();
    }
}
