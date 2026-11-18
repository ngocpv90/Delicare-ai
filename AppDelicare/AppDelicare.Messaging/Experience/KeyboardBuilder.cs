using AppDelicare.Messaging.Channels.Telegram.Models;

namespace AppDelicare.Messaging.Experience
{
    /// <summary>
    /// BOT-1.3 (khung) – BOT_TELEGRAM §1.4: bàn phím 3 nút cố định. BOT-1.4 bổ sung: ≤ 6 chức năng hiện phẳng, bàn phím theo việc đang làm.
    /// </summary>
    public static class KeyboardBuilder
    {
        public const string MyShift = "⚡ Ca của tôi";
        public const string Functions = "📂 Chức năng";
        public const string Help = "❓ Trợ giúp";
        public const string SharePhone = "📱 Xác nhận số điện thoại";

        public static TgReplyKeyboard MainMenu() => new()
        {
            Keyboard = [[new() { Text = MyShift }, new() { Text = Functions }, new() { Text = Help }]],
            IsPersistent = true,
        };

        /// <summary>Nút chia sẻ số điện thoại (Telegram xác thực số thuộc tài khoản đang chat – BOT_TELEGRAM §2.1).</summary>
        public static TgReplyKeyboard Phone() => new()
        {
            Keyboard = [[new() { Text = SharePhone, RequestContact = true }]],
            OneTimeKeyboard = true,
        };

        public static TgReplyKeyboardRemove Remove() => new();
    }
}
