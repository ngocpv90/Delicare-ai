namespace AppDelicare.Model.Helper
{
    /// <summary>
    /// UI-6.10 (D-88 c – luật CLAUDE.md §4.5 mục 1): từ điển trạng thái dùng chung phía máy chủ – khi bảng / cột chưa có định nghĩa riêng
    /// (<see cref="StatusCatalogEffective"/>) thì thống kê trạng thái, tab Liên quan, file xuất, bot tra ở đây thay vì in mã tiếng Anh.
    /// Cùng bảng với <c>UI.STATUS_VI</c> (app-delicare-client core/ui.js) và <c>docs/claude/BUSINESS_RULES.md</c> mục "Nhãn hiển thị".
    /// Active / Inactive = <b>Đang hoạt động / Ngừng hoạt động</b> mọi nơi (bỏ biến thể "Hoạt động", "Đang áp dụng", "Ngừng", "Không hoạt động").
    /// </summary>
    public static class StatusLabels
    {
        public const string Active = "Đang hoạt động";
        public const string Inactive = "Ngừng hoạt động";

        public static readonly IReadOnlyDictionary<string, string> Common = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Active"] = Active, ["Inactive"] = Inactive, ["Draft"] = "Nháp", ["Submitted"] = "Đã gửi", ["Pending"] = "Chờ xử lý",
            ["PendingApproval"] = "Chờ duyệt", ["Approved"] = "Đã duyệt", ["Rejected"] = "Từ chối", ["Cancelled"] = "Đã hủy", ["Canceled"] = "Đã hủy",
            ["Confirmed"] = "Đã xác nhận", ["Locked"] = "Đã chốt", ["Published"] = "Đã công bố", ["Superseded"] = "Bị thay thế", ["Expired"] = "Hết hiệu lực",
            ["Open"] = "Đang mở", ["Closed"] = "Đã đóng", ["InProgress"] = "Đang thực hiện", ["Completed"] = "Hoàn thành", ["Delivered"] = "Đã giao",
            ["Failed"] = "Thất bại", ["OnHold"] = "Tạm giữ", ["Hold"] = "Tạm giữ", ["Passed"] = "Đạt", ["Pass"] = "Đạt", ["Fail"] = "Không đạt",
            ["Success"] = "Thành công", ["Succeeded"] = "Thành công", ["Running"] = "Đang chạy", ["Started"] = "Đang chạy", ["Queued"] = "Chờ gửi",
            ["Sent"] = "Đã gửi", ["Received"] = "Đã nhận", ["Processed"] = "Đã xử lý", ["Ignored"] = "Bỏ qua", ["Linked"] = "Đã liên kết",
            ["Revoked"] = "Đã thu hồi", ["Unlinked"] = "Đã gỡ liên kết", ["Resigned"] = "Nghỉ việc", ["Suspended"] = "Tạm ngừng", ["New"] = "Mới",
            ["Planned"] = "Kế hoạch", ["Released"] = "Đã phát hành", ["Available"] = "Khả dụng", ["Reserved"] = "Đã giữ chỗ", ["Consumed"] = "Đã dùng hết",
            ["Returned"] = "Đã trả", ["Packed"] = "Đã đóng gói", ["Paid"] = "Đã thanh toán", ["Unpaid"] = "Chưa thanh toán", ["Valid"] = "Hợp lệ",
            ["Invalid"] = "Không hợp lệ", ["Error"] = "Lỗi", ["Warning"] = "Cảnh báo", ["Info"] = "Thông tin", ["Terminated"] = "Chấm dứt", ["READY"] = "Sẵn sàng", ["PENDING_SOURCE"] = "Chờ nghiệp vụ gốc", ["WAITING_AMIS"] = "Chờ lập trên AMIS", ["CREATED_ON_AMIS"] = "Đã lập trên AMIS", ["ERROR"] = "Lỗi", ["CHANGED"] = "Đã sửa – cần điều chỉnh", ["Exported"] = "Đã tải Excel", ["MarkedCreated"] = "Đánh dấu đã lập trên AMIS",
            ["Obsolete"] = "Hết dùng", ["Posted"] = "Đã ghi sổ", ["Issued"] = "Đã xuất", ["Matched"] = "Khớp", ["Variance"] = "Chênh lệch",
            ["DAT"] = "Đạt", ["KHONG_DAT"] = "Không đạt", ["TAM_GIU"] = "Tạm giữ", ["REJECT"] = "Loại", ["DIRTY"] = "Bẩn", ["DAMAGED"] = "Hỏng", ["CLEAN"] = "Sạch", ["GOOD"] = "Tốt", ["DELIVERY"] = "Giao hàng", ["CORE"] = "Lõi", ["RECEIVING"] = "Nhận hàng", ["STORAGE"] = "Bảo quản", ["Duplicate"] = "Trùng lặp", ["SANG"] = "Sáng", ["TRUA"] = "Trưa", ["TOI"] = "Tối", ["DEM"] = "Đêm",
            ["Added"] = "Thêm", ["Modified"] = "Sửa", ["Deleted"] = "Xóa", ["Download"] = "Tải xuống", ["Login"] = "Đăng nhập", ["Logout"] = "Đăng xuất",
            ["RoleSaveMany"] = "Đổi quyền", ["ResignScheduled"] = "Hẹn nghỉ việc", ["ResignCancelled"] = "Hủy hẹn nghỉ việc", ["Resign"] = "Nghỉ việc", ["ConfirmValid"] = "Xác nhận hợp lệ", ["StatusChange"] = "Đổi trạng thái", // UI-6.48 (hồi quy UI-6.49)
            ["Inbound"] = "Đến", ["Outbound"] = "Đi", ["True"] = "Có", ["False"] = "Không",
        };

        /// <summary>Biến thể cũ của Active / Inactive trong STATUS_CATALOG → thống nhất (D-88).</summary>
        private static readonly HashSet<string> ActiveVariants = new(StringComparer.OrdinalIgnoreCase) { "Hoạt động", "Đang áp dụng", "Đang hoạt động", "Active" };
        private static readonly HashSet<string> InactiveVariants = new(StringComparer.OrdinalIgnoreCase) { "Ngừng", "Không hoạt động", "Ngừng áp dụng", "Ngừng hoạt động", "Inactive" };

        /// <summary>Nhãn tiếng Việt của mã (không có trong từ điển → chính mã).</summary>
        public static string? Vi(string? code) => code != null && Common.TryGetValue(code, out string? v) ? v : code;

        /// <summary>Thống nhất nhãn Active / Inactive của danh mục; mã khác giữ nhãn riêng của bảng.</summary>
        public static string Unify(string code, string label)
            => code.Equals("Active", StringComparison.OrdinalIgnoreCase) && ActiveVariants.Contains(label) ? Active
               : code.Equals("Inactive", StringComparison.OrdinalIgnoreCase) && InactiveVariants.Contains(label) ? Inactive
               : label;
    }
}
