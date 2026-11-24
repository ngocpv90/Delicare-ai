using System.Text.RegularExpressions;
using AppDelicare.Messaging.Callback;

namespace AppDelicare.Messaging.Experience
{
    /// <summary>
    /// BOT-UX-2 (mục 2): danh mục callback CHỈ XEM (mở màn: danh sách con, thẻ, chọn giá trị, chọn lý do) – chỉ những callback này được đẩy vào ngăn xếp màn của panel
    /// và được phát lại khi bấm ◀ Quay lại. Callback ghi (Bắt đầu, ghi nhiệt độ, Giao, Duyệt…) KHÔNG có trong danh mục → không bao giờ bị phát lại.
    /// Thẻ được vẽ lại sau thao tác ghi tự khai báo mình bằng <see cref="Show"/> (vd. thẻ mẻ = <c>bat:view:&lt;Id&gt;</c>) → ngăn xếp lùi về đúng thẻ đó.
    /// </summary>
    public static class Screens
    {
        private static readonly Dictionary<string, HashSet<string>> Views = new(StringComparer.Ordinal)
        {
            // Bếp: thẻ mẻ, nhập nhiệt độ / số suất (màn số), chọn hành động khắc phục, lý do nấu thêm.
            [CallbackData.Kinds.Batch] = ["view", "t", "tn", "fin", "fn", "ex", "exn", "exq", "wx", "wxn", "fix"],
            // Giao vận: thẻ chuyến, thẻ điểm, chọn nhiệt độ xuất phát, số suất giao, nhiệt độ bàn giao, lý do không giao được, chọn xe, số hộp nhận về.
            [CallbackData.Kinds.Delivery] = ["r", "s", "dsp", "dtn", "dl", "dqn", "dq", "fl", "rv", "bx", "bxn"],
            // Điểm phát: số suất nhận / phục vụ.
            [CallbackData.Kinds.Reconciliation] = ["rv", "rvn", "o", "on"],
            // Duyệt: thẻ duyệt, xem chi tiết, chọn lý do từ chối.
            [CallbackData.Kinds.Approval] = ["view", "more", "no"],
            // Phản ánh: thẻ phản ánh.
            [CallbackData.Kinds.Complaint] = ["v"],
            // Nhân sự: chọn lý do chấm hộ.
            [CallbackData.Kinds.Hr] = ["px"],
            // Xin nghỉ: loại → ngày → số ngày → lý do (chỉ đổi nháp, chưa gọi api).
            ["lv"] = ["t", "d", "dd", "nn", "n"],
            // Đổi ca: ca của tôi → ca đổi / người nhận → lý do.
            ["swp"] = ["m", "g", "x", "p"],
        };

        private static readonly Regex Bold = new("<b>(.*?)</b>", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        private static readonly Regex Tag = new("<[^>]+>", RegexOptions.CultureInvariant);

        /// <summary>
        /// BOT-UX-2 (mục 1): nút quay lại TRONG NỘI DUNG chỉ khi không có panel (nhóm chat / panel tắt / thẻ việc ngoài panel) – có panel thì mỗi màn chỉ còn 1 nút
        /// ◀ Quay lại ở hàng điều hướng.
        /// </summary>
        public static IEnumerable<Presentation.NeutralButton> Back(string text, string callback)
            => PanelSession.Current == null ? [new Presentation.NeutralButton(text, callback)] : [];

        public static bool IsView(CallbackData? data) => data != null && Views.TryGetValue(data.Kind, out var a) && a.Contains(data.Action);

        public static bool IsView(string? callback) => IsView(CallbackData.Parse(callback));

        /// <summary>
        /// Khóa màn: <c>loại:hành động:&lt;phần đầu id&gt;</c> (bỏ giá trị sau dấu chấm đầu tiên) – màn số bấm −1 / +1 (đổi giá trị trong callback) vẫn là 1 màn.
        /// </summary>
        public static string KeyOf(string callback)
        {
            var d = CallbackData.Parse(callback);
            if (d == null)
            {
                return callback;
            }
            string id = d.Id ?? string.Empty;
            int dot = id.IndexOf('.');
            return $"{d.Kind}:{d.Action}:{(dot >= 0 ? id[..dot] : id)}";
        }

        /// <summary>Chức năng khai báo màn vừa vẽ (thẻ) – chỉ nhận callback trong danh mục chỉ xem.</summary>
        public static void Show(string replay)
        {
            if (PanelSession.Current is { } s && IsView(replay))
            {
                s.Screen = (replay, KeyOf(replay));
            }
        }

        /// <summary>Tên màn trên đường dẫn: tiêu đề đậm của nội dung, cắt trước " – " / " (" (vd. "🔥 ME-0123 – Cơm gà" → "🔥 ME-0123"), ≤ 28 ký tự.</summary>
        public static string? LabelOf(string? html)
        {
            if (string.IsNullOrEmpty(html) || Bold.Match(html) is not { Success: true } m)
            {
                return null;
            }
            string t = System.Net.WebUtility.HtmlDecode(Tag.Replace(m.Groups[1].Value, string.Empty)).Trim();
            foreach (string sep in new[] { " – ", " (" })
            {
                int i = t.IndexOf(sep, StringComparison.Ordinal);
                if (i > 0)
                {
                    t = t[..i];
                }
            }
            return t.Length <= 28 ? t : t[..27].TrimEnd() + "…";
        }
    }
}
