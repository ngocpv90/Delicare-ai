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

        /// <summary>Nhóm hàng của màn số (<see cref="NeutralButton.Row"/>).</summary>
        public const string StepRow = "num-step";
        public const string QuickRow = "num-quick";

        /// <summary>
        /// BOT-UX-2 (mục 6): màn nhập số không gõ tay – giá trị điền sẵn (kế hoạch / lần trước) hiện trên panel "👉 78 °C", hàng [−10] [−1] [+1] [+10], hàng chọn nhanh,
        /// [✅ Xác nhận 78 °C]. Giá trị nằm trong callback (số nguyên): đổi giá trị = <c>&lt;loại&gt;:&lt;màn&gt;:&lt;khóa&gt;.&lt;giá trị&gt;</c> (vẽ lại cùng màn, KHÔNG gọi api ghi),
        /// xác nhận = <c>&lt;loại&gt;:&lt;xác nhận&gt;:&lt;khóa&gt;.&lt;giá trị&gt;</c> (chức năng ghi). Nháp chỉ nằm trên panel – ◀ Quay lại mở lại màn trước = hủy nháp.
        /// </summary>
        public static void Number(NeutralMessage m, string kind, string screenAction, string confirmAction, string key, decimal value, string unit,
            IEnumerable<decimal>? quick = null, decimal min = 0, decimal max = 99999, string confirmLabel = "✅ Xác nhận")
        {
            int lo = (int)Math.Ceiling(min), hi = (int)Math.Floor(max);
            int v = Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), lo, hi);
            string Id(int x) => (string.IsNullOrEmpty(key) ? string.Empty : key + ".") + x.ToString(CultureInfo.InvariantCulture);
            string u = string.IsNullOrEmpty(unit) ? string.Empty : " " + unit;
            m.Lines.Add($"👉 {v}{u}");
            foreach (int s in new[] { -10, -1, 1, 10 })
            {
                int nv = Math.Clamp(v + s, lo, hi);
                m.Buttons.Add(new NeutralButton((s > 0 ? "+" : "−") + Math.Abs(s), CallbackData.Build(kind, screenAction, Id(nv)), Row: StepRow));
            }
            foreach (int q in (quick ?? []).Select(x => (int)Math.Round(x, MidpointRounding.AwayFromZero)).Where(x => x >= lo && x <= hi && x != v).Distinct().Take(NeutralMessage.MaxRowWidth))
            {
                m.Buttons.Add(new NeutralButton(q.ToString(CultureInfo.InvariantCulture), CallbackData.Build(kind, screenAction, Id(q)), Row: QuickRow));
            }
            m.Buttons.Add(new NeutralButton($"{confirmLabel} {v}{u}", CallbackData.Build(kind, confirmAction, Id(v))));
        }

        /// <summary>Giá trị số cuối của khóa callback "&lt;id&gt;.&lt;…&gt;.&lt;giá trị&gt;" (null = không có / không đọc được).</summary>
        public static decimal? Value(string? raw)
            => decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;

        /// <summary>BOT-UX-2 (mục 9): dòng kết quả đầu thẻ sau thao tác ghi: "✅ … · HH:mm" (giờ Việt Nam).</summary>
        public static string Done(string text, TimeProvider time) => "✅ " + text + " · " + Core.VnTime.Now(time).ToString("HH:mm", CultureInfo.InvariantCulture);

        private static string Fmt(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
