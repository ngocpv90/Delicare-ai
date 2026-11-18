using System.Globalization;
using AppDelicare.Messaging.Callback;
using AppDelicare.Messaging.Presentation;

namespace AppDelicare.Messaging.Experience
{
    /// <summary>
    /// BOT-1.4 (BOT_TELEGRAM §1.2 "nhập liệu không phải gõ"): nút số đặt sẵn (vd. nhiệt độ lõi [70] [72] [75] [78] [80] [Khác]), bước [−10] [−1] [+1] [+10] quanh giá trị,
    /// checklist trong MỘT tin (bấm từng mục ✅ / ❌ rồi [Hoàn tất]). Callback: <c>num:&lt;trường&gt;:&lt;giá trị&gt;</c>, <c>chk:&lt;mục&gt;:&lt;0|1&gt;</c>, <c>chk:done</c>.
    /// Dùng ở BOT-1.5 (mẻ, nhiệt độ, checklist ATTP).
    /// </summary>
    public static class InputWidgets
    {
        public const string Other = "other";

        public static List<NeutralButton> NumberPad(string field, IEnumerable<decimal> presets, bool allowOther = true)
        {
            var buttons = presets.Select(v => new NeutralButton(Fmt(v), CallbackData.Build(CallbackData.Kinds.Number, field, Fmt(v)))).ToList();
            if (allowOther)
            {
                buttons.Add(new NeutralButton("Khác", CallbackData.Build(CallbackData.Kinds.Number, field, Other)));
            }
            return buttons;
        }

        public static List<NeutralButton> Stepper(string field, decimal current, params decimal[] steps)
        {
            var list = (steps.Length == 0 ? [-10m, -1m, 1m, 10m] : steps)
                .Select(s => new NeutralButton((s > 0 ? "+" : "−") + Fmt(Math.Abs(s)), CallbackData.Build(CallbackData.Kinds.Number, field, Fmt(current + s)))).ToList();
            list.Add(new NeutralButton("✅ " + Fmt(current), CallbackData.Build(CallbackData.Kinds.Number, field, "ok" + Fmt(current))));
            return list;
        }

        /// <summary>Đọc giá trị từ callback num – null = "Khác" (hỏi gõ số) hoặc không đọc được; <paramref name="confirmed"/> = nút ✅ của Stepper.</summary>
        public static decimal? ParseNumber(CallbackData data, out bool confirmed)
        {
            string raw = data.Id ?? string.Empty;
            confirmed = raw.StartsWith("ok", StringComparison.Ordinal);
            if (confirmed)
            {
                raw = raw[2..];
            }
            return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
        }

        /// <summary>Checklist trong 1 tin: mỗi mục 1 hàng [✅ / ❌ / ⬜ tên] bấm đổi trạng thái; cuối [Hoàn tất] khi mọi mục đã chọn.</summary>
        public static NeutralMessage Checklist(string title, IReadOnlyList<(string Label, bool? Passed)> items)
        {
            var message = new NeutralMessage { Title = title };
            for (int i = 0; i < items.Count; i++)
            {
                var (label, passed) = items[i];
                string icon = passed switch { true => "✅", false => "❌", _ => "⬜" };
                // Bấm: chưa chọn → Đạt → Không đạt → Đạt…
                string next = passed == true ? "0" : "1";
                message.Buttons.Add(new NeutralButton($"{icon} {label}", CallbackData.Build(CallbackData.Kinds.Checklist, i.ToString(CultureInfo.InvariantCulture), next)));
            }
            if (items.All(x => x.Passed != null))
            {
                message.Buttons.Add(new NeutralButton("✔️ Hoàn tất", CallbackData.Build(CallbackData.Kinds.Checklist, "done")));
            }
            return message;
        }

        private static string Fmt(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
