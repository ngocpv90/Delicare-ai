using System.Text;

namespace AppDelicare.WebApi.Infrastructure
{
    /// <summary>
    /// Đọc và kiểm tra các bí mật bắt buộc lúc khởi động (T01).
    /// Bí mật KHÔNG nằm trong repo: dev dùng `dotnet user-secrets`, prod dùng biến môi trường
    /// (vd. <c>Jwt__SigningKey</c>, <c>ConnectionStrings__MainConnectionString</c>).
    /// </summary>
    public static class StartupSecrets
    {
        public const string JwtSigningKey = "Jwt:SigningKey";
        public const string JwtAccessTokenMinutes = "Jwt:AccessTokenMinutes";
        public const string MainConnectionString = "ConnectionStrings:MainConnectionString";
        public const string GemBoxLicense = "Licenses:GemBox";
        public const string QuickPulseApiKey = "ApplicationInsights:QuickPulseApiKey";

        /// <summary>HMAC-SHA256 cần khóa tối thiểu 256 bit.</summary>
        public const int MinSigningKeyBytes = 32;

        public const int DefaultAccessTokenMinutes = 120;

        /// <summary>
        /// Ném <see cref="InvalidOperationException"/> liệt kê đủ các khóa thiếu / sai, kèm cách đặt.
        /// </summary>
        public static void Validate(IConfiguration configuration)
        {
            var errors = new List<string>();

            string? signingKey = configuration[JwtSigningKey];
            if (string.IsNullOrWhiteSpace(signingKey))
            {
                errors.Add($"Thiếu '{JwtSigningKey}'.");
            }
            else if (Encoding.UTF8.GetByteCount(signingKey) < MinSigningKeyBytes)
            {
                errors.Add($"'{JwtSigningKey}' quá ngắn: cần tối thiểu {MinSigningKeyBytes} byte.");
            }

            if (string.IsNullOrWhiteSpace(configuration[MainConnectionString]))
            {
                errors.Add($"Thiếu '{MainConnectionString}'.");
            }

            string? minutes = configuration[JwtAccessTokenMinutes];
            if (!string.IsNullOrWhiteSpace(minutes) && (!int.TryParse(minutes, out int m) || m <= 0))
            {
                errors.Add($"'{JwtAccessTokenMinutes}' phải là số nguyên dương (phút).");
            }

            if (errors.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "Cấu hình bí mật chưa đủ, ứng dụng dừng khởi động:" + Environment.NewLine
                + string.Join(Environment.NewLine, errors.Select(e => " - " + e)) + Environment.NewLine
                + "Cách đặt (dev): dotnet user-secrets set \"<Khóa>\" \"<giá trị>\" --project AppDelicare/AppDelicare.WebApi" + Environment.NewLine
                + "Cách đặt (prod): biến môi trường, thay ':' bằng '__' (vd. Jwt__SigningKey)." + Environment.NewLine
                + "Xem docs/claude/SECRETS_TODO.md.");
        }

        public static byte[] GetSigningKeyBytes(IConfiguration configuration)
            => Encoding.UTF8.GetBytes(configuration[JwtSigningKey] ?? string.Empty);

        public static TimeSpan GetAccessTokenLifetime(IConfiguration configuration)
            => TimeSpan.FromMinutes(int.TryParse(configuration[JwtAccessTokenMinutes], out int m) && m > 0
                ? m
                : DefaultAccessTokenMinutes);
    }
}
