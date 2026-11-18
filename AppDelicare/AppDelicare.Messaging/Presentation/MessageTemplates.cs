using System.Text.Json;

namespace AppDelicare.Messaging.Presentation
{
    /// <summary>Nút trung tính: callback (≤ 64 byte, BOT-1.4 Callback/CallbackData) hoặc link.</summary>
    public sealed record NeutralButton(string Text, string? Callback = null, string? Url = null);

    /// <summary>Nội dung trung tính (không phụ thuộc kênh) – formatter của từng kênh dựng ra HTML Telegram / Zalo…</summary>
    public sealed class NeutralMessage
    {
        public string? Title { get; set; }
        public List<string> Lines { get; set; } = [];
        public List<NeutralButton> Buttons { get; set; } = [];
        /// <summary>URGENT / NORMAL / DIGEST – URGENT có chuông, NORMAL gửi im lặng (BOT_TELEGRAM §1.2).</summary>
        public string? Level { get; set; }

        public static NeutralMessage Text(params string[] lines) => new() { Lines = [.. lines] };
    }

    /// <summary>
    /// BOT-1.3 (D-62 §1.5): mã mẫu tin của outbox (<c>PayloadJson.template</c>) → nội dung trung tính. Câu chữ CHỈ nằm ở đây (api chỉ ghi mã + dữ liệu).
    /// Khung + mẫu đầu (BOT-1.1 NotificationCatalog); đủ danh mục ở BOT-1.7. Mẫu chưa có → tin chung "Có thông báo mới" (không mất tin).
    /// </summary>
    public static class MessageTemplates
    {
        public static NeutralMessage Render(string? payloadJson)
        {
            string? template = null, level = null;
            JsonElement data = default;
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson);
                var root = doc.RootElement.Clone();
                template = Str(root, "template");
                level = Str(root, "level");
                if (root.TryGetProperty("data", out var d))
                {
                    data = d;
                }
            }
            catch (JsonException)
            {
            }
            var m = template?.ToUpperInvariant() switch
            {
                "MANUAL_TEXT" => NeutralMessage.Text(Str(data, "text") ?? string.Empty),
                "SYSTEM_TEST" => new NeutralMessage { Title = "Kiểm tra kết nối", Lines = ["Bot Delicare đã kết nối thành công."] },
                "APPROVAL_PENDING" => new NeutralMessage { Title = "Có yêu cầu chờ bạn duyệt", Lines = Lines(data, "title", "requestNo", "requester") },
                "APPROVAL_DECIDED" => new NeutralMessage { Title = "Yêu cầu của bạn đã có kết quả", Lines = Lines(data, "title", "requestNo", "decision", "comment") },
                "SHIFT_ASSIGNED" => new NeutralMessage { Title = "Bạn được phân ca", Lines = Lines(data, "workDate", "shift", "location") },
                "FOOD_SAFETY_CRITICAL" => new NeutralMessage { Title = "Lỗi an toàn thực phẩm nghiêm trọng", Lines = Lines(data, "title", "batchNo", "detail") },
                _ => new NeutralMessage { Title = "Có thông báo mới", Lines = Lines(data, "title", "detail") },
            };
            m.Level = level;
            return m;
        }

        private static List<string> Lines(JsonElement data, params string[] keys)
            => [.. keys.Select(k => Str(data, k)).Where(v => !string.IsNullOrWhiteSpace(v)).Cast<string>()];

        private static string? Str(JsonElement e, string key)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v)
                ? v.ValueKind switch { JsonValueKind.String => v.GetString(), JsonValueKind.Null or JsonValueKind.Undefined => null, _ => v.ToString() }
                : null;
    }
}
