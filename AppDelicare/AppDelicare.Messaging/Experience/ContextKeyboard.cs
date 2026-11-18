using AppDelicare.Messaging.Channels.Telegram.Models;

namespace AppDelicare.Messaging.Experience
{
    /// <summary>
    /// BOT-1.4 (BOT_TELEGRAM §1.4): bàn phím theo việc đang làm (vd. giao hàng [📍 Đến điểm] [📷 Ảnh ký nhận] [✅ Xong điểm] [⬅️ Thoát]) – luôn có [⬅️ Thoát]
    /// trả về bàn phím thường. Nút cần vị trí / liên hệ đánh dấu trong <see cref="ContextButton"/>. Dùng ở BOT-1.5 (giao vận, chấm hộ…).
    /// </summary>
    public static class ContextKeyboard
    {
        public const string Exit = "⬅️ Thoát";

        public sealed record ContextButton(string Text, bool RequestLocation = false);

        public static TgReplyKeyboard Build(IEnumerable<ContextButton> buttons, int perRow = 2)
        {
            var list = buttons.ToList();
            var rows = list.Chunk(Math.Max(1, perRow))
                .Select(r => r.Select(b => new TgKeyboardButton { Text = b.Text, RequestLocation = b.RequestLocation ? true : null }).ToList()).ToList();
            rows.Add([new TgKeyboardButton { Text = Exit }]);
            return new TgReplyKeyboard { Keyboard = rows, IsPersistent = true };
        }
    }
}
