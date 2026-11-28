using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using AppDelicare.Model.Helper;

namespace AppDelicare.Services.Implement.Identity
{
    /// <summary>
    /// BOT-1.2 (D-62): mã liên kết Telegram – không lưu DB (không có bảng / cột mã). Nội dung 21 byte = loại (U tài khoản / E nhân viên) + Id (16 byte)
    /// + hạn (giây Unix, 4 byte) và chữ ký HMAC-SHA256 (16 byte đầu) bằng <c>Messaging:LinkSigningKey</c> → base64url 50 ký tự (Telegram <c>/start</c> cho phép ≤ 64,
    /// [A-Za-z0-9_-]). Sửa 1 ký tự → sai chữ ký; quá hạn → từ chối. Dùng 1 lần: TelegramLinkService ghi IntegrationLog LINK theo băm mã.
    /// </summary>
    public static class LinkCodeSigner
    {
        private const int PayloadLength = 21;
        private const int MacLength = 16;

        public static string Create(char kind, Guid id, DateTime expiresAtUtc, string key)
        {
            byte[] all = new byte[PayloadLength + MacLength];
            all[0] = (byte)kind;
            id.TryWriteBytes(all.AsSpan(1, 16));
            long unix = new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
            BinaryPrimitives.WriteUInt32BigEndian(all.AsSpan(17, 4), (uint)unix);
            byte[] mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), all.AsSpan(0, PayloadLength));
            mac.AsSpan(0, MacLength).CopyTo(all.AsSpan(PayloadLength));
            return Base64Url(all);
        }

        /// <returns>null = hợp lệ; khác null = lý do từ chối (thông báo tiếng Việt).</returns>
        public static string? Verify(string? code, string key, DateTime nowUtc, out char kind, out Guid id, out DateTime expiresAtUtc)
        {
            kind = default;
            id = Guid.Empty;
            expiresAtUtc = default;
            byte[]? all = FromBase64Url(code?.Trim());
            if (all == null || all.Length != PayloadLength + MacLength)
            {
                return ConstantHelper.Message.BotLinkCodeInvalid;
            }
            byte[] expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), all.AsSpan(0, PayloadLength));
            if (!CryptographicOperations.FixedTimeEquals(expected.AsSpan(0, MacLength), all.AsSpan(PayloadLength)))
            {
                return ConstantHelper.Message.BotLinkCodeInvalid;
            }
            kind = (char)all[0];
            if (kind is not (MessagingCode.LinkKind.User or MessagingCode.LinkKind.Employee or MessagingCode.LinkKind.CustomerContact))
            {
                return ConstantHelper.Message.BotLinkCodeInvalid;
            }
            id = new Guid(all.AsSpan(1, 16));
            expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadUInt32BigEndian(all.AsSpan(17, 4))).UtcDateTime;
            return nowUtc > expiresAtUtc ? ConstantHelper.Message.BotLinkCodeExpired : null;
        }

        /// <summary>Khóa chống dùng lại (IntegrationLog.IdempotencyKey) – băm, không lưu mã gốc.</summary>
        public static string UsedKey(string code) => "LC:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim())))[..40];

        private static string Base64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static byte[]? FromBase64Url(string? s)
        {
            if (string.IsNullOrEmpty(s) || s.Length > 64 || s.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_')))
            {
                return null;
            }
            string b = s.Replace('-', '+').Replace('_', '/');
            b = b.PadRight(b.Length + (4 - b.Length % 4) % 4, '=');
            try
            {
                return Convert.FromBase64String(b);
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
