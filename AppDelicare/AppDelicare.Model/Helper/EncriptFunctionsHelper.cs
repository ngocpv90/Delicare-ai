using System.Security.Cryptography;
using System.Text;

namespace AppDelicare.Model.Helper
{
    /// <summary>
    /// Băm mật khẩu (T48.0, D-29). Hash mới: PBKDF2-SHA256, salt ngẫu nhiên 16 byte, 100.000 vòng, lưu tự mô tả trong <c>User.PassWord</c>
    /// dạng <c>PBKDF2$&lt;vòng&gt;$&lt;salt base64&gt;$&lt;hash base64&gt;</c> (cột nvarchar(max) – không thêm cột).
    /// Hash MD5 cũ vẫn kiểm được để user cũ đăng nhập; đúng thì gọi nơi đăng nhập nâng lên PBKDF2 (<see cref="VerifyPassword(string, string?, string?, out bool)"/>).
    /// </summary>
    public static class EncriptFunctionsHelper
    {
        public const string Pbkdf2Prefix = "PBKDF2$";
        public const int Pbkdf2Iterations = 100_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        /// <summary>MD5 kiểu cũ (hex có gạch). Chỉ còn dùng để kiểm hash cũ – không dùng để lưu mật khẩu mới.</summary>
        public static string GenerateMd5String(string strChange)
        {
            byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(strChange));
            return BitConverter.ToString(hash);
        }

        public static bool VerifyMd5String(string str, string hash)
        {
            var hashinput = GenerateMd5String(str);
            StringComparer comparer = StringComparer.OrdinalIgnoreCase;
            return 0 == comparer.Compare(hashinput, hash);
        }

        /// <summary>T48.0: hash mật khẩu để LƯU – luôn PBKDF2 (salt mới mỗi lần nên 2 user cùng mật khẩu có hash khác nhau).</summary>
        public static string GeneratePassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, HashSize);
            return string.Concat(Pbkdf2Prefix, Pbkdf2Iterations, "$", Convert.ToBase64String(salt), "$", Convert.ToBase64String(hash));
        }

        public static bool IsPbkdf2(string? stored) => stored != null && stored.StartsWith(Pbkdf2Prefix, StringComparison.Ordinal);

        /// <summary>Kiểm mật khẩu với chuỗi đã lưu (PBKDF2 hoặc MD5 cũ).</summary>
        public static bool VerifyPassword(string password, string hash) => VerifyPassword(password, hash, null, out _);

        /// <summary>
        /// Kiểm mật khẩu. PBKDF2: so sánh thời gian hằng. MD5 cũ: chấp nhận <c>MD5(mật khẩu)</c> (đăng nhập cũ) và <c>MD5(mật khẩu + tên đăng nhập)</c>
        /// (mật khẩu đổi qua ChangePassword cũ) – khi đó <paramref name="needsUpgrade"/> = true để nơi gọi ghi lại hash PBKDF2.
        /// </summary>
        public static bool VerifyPassword(string password, string? stored, string? userName, out bool needsUpgrade)
        {
            needsUpgrade = false;
            if (string.IsNullOrEmpty(stored))
            {
                return false;
            }
            if (IsPbkdf2(stored))
            {
                string[] parts = stored.Split('$');
                if (parts.Length != 4 || !int.TryParse(parts[1], out int iterations) || iterations <= 0)
                {
                    return false;
                }
                try
                {
                    byte[] salt = Convert.FromBase64String(parts[2]);
                    byte[] expected = Convert.FromBase64String(parts[3]);
                    byte[] actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                    return CryptographicOperations.FixedTimeEquals(actual, expected);
                }
                catch (FormatException)
                {
                    return false;
                }
            }
            bool legacy = VerifyMd5String(password, stored)
                || (!string.IsNullOrEmpty(userName) && VerifyMd5String(string.Concat(password, userName), stored));
            needsUpgrade = legacy;
            return legacy;
        }
    }
}
