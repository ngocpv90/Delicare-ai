using System.Globalization;
using AppDelicare.Messaging.Presentation;
using Microsoft.Extensions.Configuration;

namespace AppDelicare.Messaging.Experience
{
    /// <summary>
    /// BOT-UX-2 (mục 7): lý do / ghi chú hay gặp của từng luồng = nút bấm (không gõ tay). Cấu hình <c>Messaging:Reasons:&lt;luồng&gt;</c> (mảng chuỗi, tối đa 8);
    /// không cấu hình → danh sách mặc định bên dưới. Nút cuối [✏️ Khác…] mới hỏi gõ tay (ForceReply). Nút mang SỐ THỨ TỰ lý do (callback ≤ 64 byte);
    /// đổi cấu hình giữa chừng thì số thứ tự cũ có thể lệch – chỉ dùng cho lý do / ghi chú, api vẫn kiểm nghiệp vụ.
    /// </summary>
    public sealed class ReasonCatalog(IConfiguration configuration)
    {
        public const string Section = "Messaging:Reasons";
        public const int Max = 8;
        public const string Other = "✏️ Khác…";

        /// <summary>Từ chối duyệt.</summary>
        public const string ApprovalReject = "apv-reject";
        /// <summary>Hành động khắc phục khi nhiệt độ lõi dưới ngưỡng.</summary>
        public const string CoreTempFix = "bat-fix";
        /// <summary>Lý do nấu thêm.</summary>
        public const string ExtraBatch = "bat-exr";
        /// <summary>Không giao được.</summary>
        public const string DeliveryFail = "dlv-fail";
        /// <summary>Chấm hộ vào ca.</summary>
        public const string ProxyCheckIn = "hr-proxy";
        /// <summary>Lý do xin nghỉ.</summary>
        public const string Leave = "leave-reason";
        /// <summary>Lý do đổi / nhường ca.</summary>
        public const string Swap = "swap-reason";
        /// <summary>Lý do đề xuất đổi món.</summary>
        public const string DishChange = "op-change-reason";
        /// <summary>Lô vệ sinh hộp khay không đạt.</summary>
        public const string CleaningReject = "op-clean-reject";

        private static readonly Dictionary<string, string[]> Defaults = new(StringComparer.OrdinalIgnoreCase)
        {
            [ApprovalReject] = ["Thiếu thông tin / chứng từ", "Sai số liệu – lập lại", "Chưa đúng quy định", "Không bố trí được – chọn thời gian khác"],
            [CoreTempFix] = ["Gia nhiệt lại 10 phút, đo lại", "Kéo dài thời gian nấu, đo lại", "Chia nhỏ mẻ, nấu lại", "Giữ mẻ, báo QC xử lý"],
            [ExtraBatch] = ["Khách đặt thêm suất", "Mẻ trước hỏng / không đạt", "Bù suất giao thiếu", "Hao hụt khi chia suất"],
            [DeliveryFail] = ["Khách không nhận hàng", "Không liên lạc được người nhận", "Sai / không tìm thấy địa chỉ", "Xe hỏng / trễ quá giờ"],
            [ProxyCheckIn] = ["Quên điện thoại", "Điện thoại hết pin / hỏng", "Không định vị được", "Làm việc ngoài điểm"],
            [Leave] = ["Việc gia đình", "Ốm / khám bệnh", "Việc cá nhân", "Nghỉ phép năm theo kế hoạch"],
            [Swap] = ["Việc gia đình", "Ốm / khám bệnh", "Trùng lịch học / việc riêng", "Đã thỏa thuận với đồng nghiệp"],
            [DishChange] = ["Khách đề nghị", "Thiếu nguyên liệu", "Giá nguyên liệu tăng", "Món không hợp mùa"],
            [CleaningReject] = ["Còn bẩn / bám dầu", "Nhiệt độ rửa không đạt", "Hộp khay hỏng / nứt", "Thiếu hóa chất đúng quy định"],
        };

        public IReadOnlyList<string> For(string flow)
        {
            var configured = configuration.GetSection(Section + ":" + flow).GetChildren()
                .Select(c => c.Value?.Trim()).Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).Take(Max).ToList();
            return configured.Count > 0 ? configured : Defaults.TryGetValue(flow, out var d) ? d : [];
        }

        /// <summary>Lý do theo số thứ tự (từ callback) – null = không còn (cấu hình đổi).</summary>
        public string? At(string flow, string? index)
            => int.TryParse(index, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) && For(flow) is { } list && i >= 0 && i < list.Count ? list[i] : null;

        /// <summary>
        /// Nút lý do: mỗi lý do 1 nút <c>&lt;loại&gt;:&lt;hành động chọn&gt;:&lt;khóa&gt;.&lt;số thứ tự&gt;</c> + [✏️ Khác…] <c>&lt;loại&gt;:&lt;hành động gõ&gt;:&lt;khóa&gt;</c>.
        /// </summary>
        public List<NeutralButton> Buttons(string flow, string kind, string pickAction, string otherAction, string key)
        {
            var list = For(flow).Select((r, i) => new NeutralButton(r, Callback.CallbackData.Build(kind, pickAction, key + "." + i.ToString(CultureInfo.InvariantCulture)))).ToList();
            list.Add(new NeutralButton(Other, Callback.CallbackData.Build(kind, otherAction, key)));
            return list;
        }
    }
}
