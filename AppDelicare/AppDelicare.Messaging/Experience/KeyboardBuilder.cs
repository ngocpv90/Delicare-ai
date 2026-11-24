using AppDelicare.Messaging.Audiences.Internal;
using AppDelicare.Messaging.Channels.Telegram.Models;

namespace AppDelicare.Messaging.Experience
{
    /// <summary>
    /// BOT-1.4 (BOT_TELEGRAM §1.4): bàn phím dưới. Người có > 6 chức năng (kể cả admin toàn quyền) → <b>3 nút cố định</b> [⚡ Ca của tôi] [📂 Chức năng] [📍 Chấm công]
    /// – tới mọi chức năng ≤ 2 lần bấm qua 📂 (phân hệ → chức năng). Người ≤ 6 chức năng → hiện thẳng các chức năng (không tầng).
    /// Nút 📍 Chấm công xin vị trí (request_location) – chỉ bàn phím dưới mới gửi được vị trí. BOT-UX-2 (mục 5): bỏ bàn phím 1 nút vị trí (đè mất 3 phím) – /chamcong chỉ nhắc bấm 📍.
    /// </summary>
    public static class KeyboardBuilder
    {
        public const string MyShift = "⚡ Ca của tôi";
        public const string Functions = "📂 Chức năng";
        public const string CheckIn = "📍 Chấm công";
        public const string Help = "❓ Trợ giúp";
        public const string SharePhone = "📱 Xác nhận số điện thoại";
        public const int FlatMax = 6;

        /// <summary>Nhãn chức năng cần xin vị trí khi bấm.</summary>
        private static bool RequestsLocation(string label) => label == CheckIn;

        /// <summary>3 nút cố định.</summary>
        public static TgReplyKeyboard MainMenu() => new()
        {
            Keyboard = [[new() { Text = MyShift }, new() { Text = Functions }, new() { Text = CheckIn, RequestLocation = true }]],
            IsPersistent = true,
        };

        /// <summary>Bàn phím theo số chức năng người đó dùng được.</summary>
        public static TgReplyKeyboard ForActions(IReadOnlyList<BotAction> actions)
        {
            if (actions.Count == 0 || actions.Count > FlatMax)
            {
                return MainMenu();
            }
            return new TgReplyKeyboard
            {
                Keyboard = [.. actions.Chunk(2).Select(row => row.Select(a => new TgKeyboardButton { Text = a.Label, RequestLocation = RequestsLocation(a.Label) ? true : null }).ToList())],
                IsPersistent = true,
            };
        }

        /// <summary>Nút chia sẻ số điện thoại (Telegram xác thực số thuộc tài khoản đang chat – BOT_TELEGRAM §2.1).</summary>
        public static TgReplyKeyboard Phone() => new()
        {
            Keyboard = [[new() { Text = SharePhone, RequestContact = true }]],
            OneTimeKeyboard = true,
        };

        public static TgReplyKeyboardRemove Remove() => new();
    }
}
