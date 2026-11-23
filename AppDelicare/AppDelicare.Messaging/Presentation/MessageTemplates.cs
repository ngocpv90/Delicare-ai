using System.Globalization;
using System.Text;
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
    /// BOT-1.7: đủ danh mục BOT_TELEGRAM §5.1 + nút của tin (<c>PayloadJson.buttons</c> = [{ text, callback | url }], callback ≤ 64 byte). Mẫu chưa có câu riêng
    /// → tiêu đề theo mã + các dòng chung (<c>data.title</c>, <c>data.detail</c>, <c>data.lines[]</c>) – không mất tin.
    /// </summary>
    public static class MessageTemplates
    {
        /// <summary>Tiêu đề mặc định theo mã mẫu (mẫu không có câu riêng).</summary>
        private static readonly Dictionary<string, string> Titles = new(StringComparer.OrdinalIgnoreCase)
        {
            ["BATCH_LATE"] = "⏰ Mẻ trễ giờ",
            ["CORE_TEMP_LOW"] = "🌡 Nhiệt độ lõi chưa đạt",
            ["FOODSAFETY_CRITICAL"] = "🚨 Lỗi an toàn thực phẩm trọng yếu",
            ["FOOD_SAFETY_CRITICAL"] = "🚨 Lỗi an toàn thực phẩm trọng yếu",
            ["IOT_OUT_OF_RANGE"] = "🌡 Nhiệt độ ngoài ngưỡng",
            ["CHECKLIST_DUE"] = "🧽 Checklist sắp đến hạn",
            ["PLAN_READY"] = "📋 Kế hoạch đã lập – chờ duyệt",
            ["SHORTAGE_MRP"] = "⚠️ Thiếu nguyên liệu sau MRP",
            ["STOCK_LOW"] = "📦 Dưới tồn tối thiểu",
            ["LOT_EXPIRING"] = "⌛ Lô cận / hết hạn",
            ["PO_LATE"] = "🧾 PO trễ hẹn",
            ["QC_INCOMING_FAIL"] = "⛔ QC đầu vào không đạt",
            ["TRIP_NOT_DEPARTED"] = "🚚 Xe chưa rời bếp",
            ["TRIP_DEPARTED"] = "🚚 Xe đã xuất phát",
            ["STOP_LATE"] = "⏰ Điểm giao trễ",
            ["DELIVERY_TEMP_LOW"] = "🌡 Nhiệt độ giao thấp",
            ["SHORTAGE_REPORTED"] = "🍱 Báo thiếu suất",
            ["COMPLAINT_NEW"] = "📣 Phản ánh mới",
            ["COMPLAINT_SLA"] = "⏱ Phản ánh sắp quá hạn",
            ["CONTAINER_LOSS"] = "📦 Thất thoát hộp khay",
            ["ORDERS_UNCONFIRMED"] = "⏰ Khách chưa chốt đơn",
            ["MENU_MISSING"] = "🍱 Chưa có thực đơn chung tuần sau",
            ["ORDER_WEEK_MISSING"] = "📝 Khách chưa đăng ký suất tuần sau",
            ["FOOD_COST_OVER"] = "💸 Thực đơn vượt FC % do giá NCC đổi",
            ["MENU_PUBLISHED"] = "📅 Thực đơn đã công bố",
            ["NOT_CHECKED_IN"] = "👥 Chưa vào ca",
            ["MORNING_BRIEF"] = "☀️ Bản tin sáng",
            ["DAILY_REPORT"] = "📊 Báo cáo cuối ngày",
            ["PERIOD_REPORT"] = "📊 Báo cáo kỳ",
            ["SYSTEM_JOB_FAILED"] = "🖥 Job tự động lỗi",
            ["OUTBOUND_FAILING"] = "🖥 Tin gửi lỗi nhiều lần",
            ["INCIDENT_UNCLAIMED"] = "🚨 Sự việc chưa ai nhận",
            ["LEAVE_HANDOVER"] = "🏖 Sắp nghỉ – còn việc chờ bạn duyệt",
            ["APPROVAL_OVERDUE"] = "⏰ Việc duyệt quá hạn",
            ["INGREDIENTS_TONIGHT"] = "🥬 Nguyên liệu cho ngày mai",
        };

        /// <summary>BOT-2.1: mẫu được ghim đầu chat sau khi gửi.</summary>
        public static bool IsPinned(string? template) => template?.ToUpperInvariant() is "SHIFT_CARD" or "DAY_CARD";

        public static NeutralMessage Render(string? payloadJson)
        {
            string? template = null, level = null;
            JsonElement data = default, buttons = default, incident = default;
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
                if (root.TryGetProperty("buttons", out var b))
                {
                    buttons = b;
                }
                if (root.TryGetProperty("incident", out var inc))
                {
                    incident = inc;
                }
            }
            catch (JsonException)
            {
            }
            string code = template?.ToUpperInvariant() ?? string.Empty;
            var m = code switch
            {
                "MANUAL_TEXT" => NeutralMessage.Text(Str(data, "text") ?? string.Empty),
                "SYSTEM_TEST" => new NeutralMessage { Title = "Kiểm tra kết nối", Lines = ["Bot Delicare đã kết nối thành công."] },
                "APPROVAL_PENDING" => new NeutralMessage
                {
                    Title = "⏳ Chờ bạn duyệt" + (Str(data, "requestNo") is { } no ? ": " + no : string.Empty),
                    Lines = [.. Present(Str(data, "title"), Prefixed("Người gửi: ", Str(data, "requester")), Prefixed("Cấp: ", Str(data, "step")), Prefixed("Hạn: ", When(Str(data, "dueAt"))))],
                },
                "APPROVAL_RESULT" => new NeutralMessage
                {
                    Title = (Flag(data, "approved") ? "✅ Đã duyệt" : "❌ Bị từ chối") + (Str(data, "requestNo") is { } no2 ? ": " + no2 : string.Empty),
                    Lines = [.. Present(Str(data, "title"), Prefixed("Ghi chú: ", Str(data, "comment")))],
                },
                "APPROVAL_DECIDED" => new NeutralMessage { Title = "Yêu cầu của bạn đã có kết quả", Lines = Lines(data, "title", "requestNo", "decision", "comment") },
                "SHIFT_CONFIRMED" => new NeutralMessage { Title = $"📅 Lịch ca đã xác nhận ({Str(data, "count") ?? "0"} ca)", Lines = Array(data, "lines") },
                "SHIFT_ASSIGNED" => new NeutralMessage { Title = "Bạn được phân ca", Lines = Lines(data, "workDate", "shift", "location") },
                "SHIFT_CARD" or "DAY_CARD" => new NeutralMessage { Title = Str(data, "title") ?? "🗓 Thẻ ca", Lines = Array(data, "lines") },
                _ => new NeutralMessage
                {
                    Title = Titles.GetValueOrDefault(code, "Có thông báo mới"),
                    Lines = [.. Lines(data, "title", "detail"), .. Array(data, "lines")],
                },
            };
            m.Level = level;
            m.Buttons.AddRange(Buttons(buttons));
            Incident(m, incident);
            return m;
        }

        /// <summary>BOT-2.2: trạng thái sự việc (PayloadJson.incident) – người nhận, ghi chú, đã xử lý; bỏ nút không còn hợp lệ (đã nhận → bỏ 🙋; xong → bỏ mọi nút sự việc).</summary>
        private static void Incident(NeutralMessage m, JsonElement incident)
        {
            if (incident.ValueKind != JsonValueKind.Object)
            {
                return;
            }
            if (Str(incident, "claimedBy") is { } by)
            {
                m.Lines.Add($"🙋 {by} đã nhận xử lý lúc {Str(incident, "claimedAt")}");
                m.Buttons.RemoveAll(b => b.Callback == "inc:claim");
            }
            if (incident.TryGetProperty("notes", out var notes) && notes.ValueKind == JsonValueKind.Array)
            {
                m.Lines.AddRange(notes.EnumerateArray().Select(n => $"💬 {Str(n, "at")} {Str(n, "by")}: {Str(n, "text")}"));
            }
            if (Str(incident, "resolvedBy") is { } done)
            {
                m.Lines.Add($"✅ Đã xử lý – {done} lúc {Str(incident, "resolvedAt")}");
                m.Buttons.RemoveAll(b => b.Callback?.StartsWith("inc:", StringComparison.Ordinal) == true);
            }
        }

        /// <summary>Nút của tin: bỏ nút không hợp lệ (thiếu chữ, callback &gt; 64 byte, không có callback / link).</summary>
        private static IEnumerable<NeutralButton> Buttons(JsonElement buttons)
        {
            if (buttons.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }
            foreach (var b in buttons.EnumerateArray().Take(8))
            {
                string? text = Str(b, "text"), callback = Str(b, "callback"), url = Str(b, "url");
                if (string.IsNullOrWhiteSpace(text) || (callback == null && url == null) || (callback != null && Encoding.UTF8.GetByteCount(callback) > 64))
                {
                    continue;
                }
                yield return new NeutralButton(text, callback, callback == null ? url : null);
            }
        }

        private static List<string> Lines(JsonElement data, params string[] keys)
            => [.. keys.Select(k => Str(data, k)).Where(v => !string.IsNullOrWhiteSpace(v)).Cast<string>()];

        private static List<string> Array(JsonElement data, string key)
            => data.ValueKind == JsonValueKind.Object && data.TryGetProperty(key, out var a) && a.ValueKind == JsonValueKind.Array
                ? [.. a.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().Take(20)]
                : [];

        private static IEnumerable<string> Present(params string?[] values) => values.Where(v => !string.IsNullOrWhiteSpace(v)).Cast<string>();

        private static string? Prefixed(string prefix, string? value) => string.IsNullOrWhiteSpace(value) ? null : prefix + value;

        private static string? When(string? s) => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.ToString("HH:mm dd/MM") : null;

        private static bool Flag(JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;

        private static string? Str(JsonElement e, string key)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v)
                ? v.ValueKind switch { JsonValueKind.String => v.GetString(), JsonValueKind.Null or JsonValueKind.Undefined => null, _ => v.ToString() }
                : null;
    }
}
