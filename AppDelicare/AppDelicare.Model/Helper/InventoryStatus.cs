namespace AppDelicare.Model.Helper
{
    /// <summary>T16 – trạng thái lô nguyên liệu (STATUS_CATALOG: MaterialLot.Status, PROPOSED – BA/PO CONFIRM).</summary>
    public static class MaterialLotStatus
    {
        public const string Available = "Available";
        public const string OnHold = "OnHold";
        public const string Expired = "Expired";
        public const string Consumed = "Consumed";

        public static string Label(string? status) => status switch
        {
            Available => "Khả dụng",
            OnHold => "Tạm giữ",
            Expired => "Hết hạn",
            Consumed => "Đã dùng hết",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>
    /// T16 – kết quả QC của lô (<c>MaterialLot.QcStatus</c>). Nguồn: STATUS_CATALOG (PASS / HOLD / REJECT) và BUSINESS_RULES §4
    /// (DAT / DAT_MOT_PHAN / KHONG_DAT). Lô bị chặn xuất: KHONG_DAT, REJECT, HOLD (PROPOSED – BA/PO CONFIRM: HOLD = đang tạm giữ chờ QC).
    /// </summary>
    public static class LotQcStatus
    {
        public const string Pass = "PASS";
        public const string Hold = "HOLD";
        public const string Reject = "REJECT";
        public const string Dat = "DAT";
        public const string DatMotPhan = "DAT_MOT_PHAN";
        public const string KhongDat = "KHONG_DAT";

        /// <summary>Kết quả QC không cho xuất (so sánh không phân biệt hoa thường).</summary>
        public static readonly IReadOnlySet<string> Blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { KhongDat, Reject, Hold };

        /// <summary>Kết quả QC "không đạt" – không được sửa ngược lại qua CRUD (chỉ qua luồng QC T15).</summary>
        public static readonly IReadOnlySet<string> Failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { KhongDat, Reject };

        public static bool IsBlocked(string? qcStatus) => !string.IsNullOrWhiteSpace(qcStatus) && Blocked.Contains(qcStatus.Trim());
    }

    /// <summary>T16 – trạng thái phiếu xuất kho (STATUS_CATALOG: MaterialIssue.Status, PROPOSED – BA/PO CONFIRM).</summary>
    public static class MaterialIssueStatus
    {
        public const string Draft = "Draft";
        public const string Issued = "Issued";
        public const string Cancelled = "Cancelled";

        public static string Label(string? status) => status switch
        {
            Draft => "Nháp",
            Issued => "Đã xuất",
            Cancelled => "Hủy",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>T16 – phiếu kiểm kê (STATUS_CATALOG: StockCount.Status / CountType, PROPOSED – BA/PO CONFIRM).</summary>
    public static class StockCountStatus
    {
        public const string Draft = "Draft";
        public const string Counting = "Counting";
        public const string PendingApproval = "PendingApproval";
        public const string Posted = "Posted";

        /// <summary>BE2.3 (spec 7.8 submit, 2.3): BusinessType của ApprovalRequest khi duyệt kiểm kê lệch &gt; ngưỡng.</summary>
        public const string BusinessType = "STOCK_COUNT";
        /// <summary>Cấp 1 – Quản lý kho (mục <c>#/wf-inbox?type=scm-stockcount</c>, đã có từ T07).</summary>
        public const string ScreenId = "scm-stockcount";
        /// <summary>Cấp 2 – Kế toán (mục <c>#/wf-inbox?type=scm-stockcount-l2</c>, spec 2.3) – PROPOSED.</summary>
        public const string Level2ScreenId = "scm-stockcount-l2";

        public static readonly IReadOnlyList<string> CountTypes = ["DAILY", "WEEKLY", "MONTHLY", "ADHOC"];

        public static string Label(string? status) => status switch
        {
            Draft => "Nháp",
            Counting => "Đang kiểm",
            PendingApproval => "Chờ duyệt",
            Posted => "Đã ghi sổ",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>BE2.3 (spec 7.6 adjust, GAP-F-04): duyệt điều chỉnh tồn tay STOCK_ADJUSTMENT (không có bảng phiếu điều chỉnh).</summary>
    public static class StockAdjustmentStatus
    {
        public const string BusinessType = "STOCK_ADJUSTMENT";
        /// <summary>Mục quyền duyệt <c>#/wf-inbox?type=scm-adjustment</c> "Duyệt – Phiếu điều chỉnh tồn" (đã có từ T07).</summary>
        public const string ScreenId = "scm-adjustment";
    }

    /// <summary>T16 – loại chứng từ nguồn của bút toán kho (<c>InventoryTransaction.ReferenceType</c>, PROPOSED – BA/PO CONFIRM).</summary>
    public static class InventoryReferenceType
    {
        public const string MaterialIssue = "MaterialIssue";
        public const string StockCount = "StockCount";
        public const string ManualAdjustment = "ManualAdjustment";
        public const string Transfer = "Transfer";
    }
}
