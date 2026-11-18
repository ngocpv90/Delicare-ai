using System.Security.Cryptography;
using System.Text;

namespace AppDelicare.Messaging.Channels.Telegram
{
    /// <summary>BOT-1.3: header <c>X-Telegram-Bot-Api-Secret-Token</c> = secret đã đặt khi setWebhook – so thời gian hằng. Secret trống = từ chối mọi webhook.</summary>
    public static class TelegramSecretValidator
    {
        public const string HeaderName = "X-Telegram-Bot-Api-Secret-Token";

        public static bool IsValid(string? expected, string? presented)
        {
            if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(presented))
            {
                return false;
            }
            byte[] a = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
            byte[] b = SHA256.HashData(Encoding.UTF8.GetBytes(presented));
            return CryptographicOperations.FixedTimeEquals(a, b);
        }
    }
}
