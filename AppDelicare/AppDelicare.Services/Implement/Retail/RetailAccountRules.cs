using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AppDelicare.Services.Implement.Retail
{
    /// <summary>
    /// RETAIL-2 (D-136): mật khẩu + phiên đăng nhập của khách lẻ – TÁCH khỏi JWT nhân viên (khách không bao giờ vào được API nội bộ).
    /// Mật khẩu: PBKDF2-SHA256 100.000 vòng, salt 16 byte (thư viện .NET có sẵn, không thêm gói).
    /// Phiên: <c>&lt;Id&gt;.&lt;hết hạn unix&gt;.&lt;dấu mật khẩu&gt;.&lt;HMAC&gt;</c> – khóa ký suy ra từ <c>Jwt:SigningKey</c> theo nhãn riêng;
    /// đổi mật khẩu → dấu đổi → mọi phiên cũ hết hiệu lực.
    /// </summary>
    public static class RetailAccountRules
    {
        public const string TokenHeader = "X-Retail-Token";
        private const int Iterations = 100_000;
        private const string Scheme = "PBKDF2";
        private static readonly byte[] KeyLabel = Encoding.UTF8.GetBytes("delicare-retail-customer-session-v1");

        public static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, 32);
            return string.Join('$', Scheme, Iterations.ToString(CultureInfo.InvariantCulture), Convert.ToBase64String(salt), Convert.ToBase64String(hash));
        }

        public static bool VerifyPassword(string? password, string? stored)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored))
            {
                return false;
            }
            string[] p = stored.Split('$');
            if (p.Length != 4 || p[0] != Scheme || !int.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out int iter) || iter < 1)
            {
                return false;
            }
            try
            {
                byte[] salt = Convert.FromBase64String(p[2]);
                byte[] expected = Convert.FromBase64String(p[3]);
                byte[] actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iter, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>Dấu mật khẩu: 8 ký tự đầu HMAC của mã băm – đổi mật khẩu là đổi dấu (không đưa mã băm vào phiên).</summary>
        public static string Stamp(byte[] key, string? passwordHash)
            => B64(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes("stamp|" + passwordHash)))[..8];

        /// <summary>Khóa ký phiên khách lẻ, suy ra từ khóa JWT (không dùng thẳng khóa JWT).</summary>
        public static byte[]? DeriveKey(string? jwtSigningKey)
            => string.IsNullOrWhiteSpace(jwtSigningKey) ? null : HMACSHA256.HashData(Encoding.UTF8.GetBytes(jwtSigningKey), KeyLabel);

        public static string IssueToken(byte[] key, Guid customerId, string? passwordHash, DateTime expiresAt)
        {
            long exp = new DateTimeOffset(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc)).ToUnixTimeSeconds();
            string payload = string.Join('.', customerId.ToString("N"), exp.ToString(CultureInfo.InvariantCulture), Stamp(key, passwordHash));
            return payload + "." + Sign(key, payload);
        }

        /// <summary>Đọc phiên: sai chữ ký / hết hạn → null. <paramref name="now"/> theo cùng mốc giờ đã dùng khi cấp.</summary>
        public static (Guid CustomerId, string Stamp)? ReadToken(byte[] key, string? token, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 200)
            {
                return null;
            }
            string[] p = token.Trim().Split('.');
            if (p.Length != 4 || !Guid.TryParseExact(p[0], "N", out Guid id) || !long.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out long exp))
            {
                return null;
            }
            byte[] expected = Encoding.ASCII.GetBytes(Sign(key, string.Join('.', p[0], p[1], p[2])));
            if (!CryptographicOperations.FixedTimeEquals(expected, Encoding.ASCII.GetBytes(p[3])))
            {
                return null;
            }
            long nowUnix = new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc)).ToUnixTimeSeconds();
            return exp < nowUnix ? null : (id, p[2]);
        }

        private static string Sign(byte[] key, string payload) => B64(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload)));

        private static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
