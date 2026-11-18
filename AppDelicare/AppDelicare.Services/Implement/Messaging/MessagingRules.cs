using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AppDelicare.Model.Option;

namespace AppDelicare.Services.Implement.Messaging
{
    /// <summary>
    /// BOT-1.1 (D-62): quy tắc dùng chung của kênh tin nhắn – giãn cách thử lại, cắt chuỗi lỗi, băm payload, che bí mật trong JSON cấu hình.
    /// Giờ yên lặng theo ca (D-62 p7) làm ở BOT-2.3; ở đây chỉ đọc <c>notBefore</c> người gọi truyền vào.
    /// </summary>
    public static class MessagingRules
    {
        public const string Masked = "***";

        /// <summary>Số phút chờ trước lần thử thứ <paramref name="failures"/> + 1 (đã lỗi <paramref name="failures"/> lần); null = hết lượt thử.</summary>
        public static int? RetryDelayMinutes(MessagingOptions options, int failures)
        {
            var delays = options.EffectiveRetryDelays;
            if (failures <= 0)
            {
                return 0;
            }
            return failures <= delays.Count ? Math.Max(0, delays[failures - 1]) : null;
        }

        public static string? Truncate(string? value, int max)
            => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];

        public static string PayloadHash(string? payload)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload ?? string.Empty)));

        /// <summary>Tên khóa coi là bí mật trong ConfigJson (D-62: <c>*secret*</c>, <c>*token*</c>, <c>*key*</c>) + mật khẩu.</summary>
        public static bool IsSecretKey(string name)
        {
            string n = name.ToLowerInvariant();
            return n.Contains("secret") || n.Contains("token") || n.Contains("key") || n.Contains("password") || n.Contains("pwd");
        }

        /// <summary>
        /// Che giá trị các khóa bí mật (mọi cấp) trong chuỗi JSON. Chuỗi không phải JSON hợp lệ → che toàn bộ (không đoán được chỗ nào là bí mật).
        /// </summary>
        public static string? MaskJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return json;
            }
            JsonNode? root;
            try
            {
                root = JsonNode.Parse(json);
            }
            catch (JsonException)
            {
                return Masked;
            }
            if (root == null)
            {
                return json;
            }
            MaskNode(root);
            return root.ToJsonString();
        }

        private static void MaskNode(JsonNode node)
        {
            switch (node)
            {
                case JsonObject obj:
                    foreach (var key in obj.Select(p => p.Key).ToList())
                    {
                        if (IsSecretKey(key))
                        {
                            obj[key] = Masked;
                        }
                        else if (obj[key] is { } child)
                        {
                            MaskNode(child);
                        }
                    }
                    break;
                case JsonArray arr:
                    foreach (var item in arr)
                    {
                        if (item != null)
                        {
                            MaskNode(item);
                        }
                    }
                    break;
            }
        }
    }
}
