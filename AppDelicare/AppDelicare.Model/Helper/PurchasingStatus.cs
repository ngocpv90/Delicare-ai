namespace AppDelicare.Model.Helper
{
    /// <summary>T14 – trạng thái dòng nhu cầu NL (STATUS_CATALOG: MaterialRequirement.Status, PROPOSED – BA/PO CONFIRM).</summary>
    public static class MaterialRequirementStatus
    {
        public const string Calculated = "Calculated";
        public const string Shortage = "Shortage";
        public const string Sufficient = "Sufficient";
        public const string Requested = "Requested";

        public static string Label(string? status) => status switch
        {
            Calculated => "Đã tính",
            Shortage => "Thiếu",
            Sufficient => "Đủ",
            Requested => "Đã đề nghị mua",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>T14 – trạng thái đề nghị mua (STATUS_CATALOG: PurchaseRequest.Status, PROPOSED – BA/PO CONFIRM).</summary>
    public static class PurchaseRequestStatus
    {
        public const string Draft = "Draft";
        public const string PendingApproval = "PendingApproval";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Converted = "Converted";
        public const string Cancelled = "Cancelled";

        /// <summary>BE2.3 (D-46): BusinessType của ApprovalRequest khi duyệt đề nghị mua (handler <c>PurchaseRequestApprovalHandler</c>).</summary>
        public const string BusinessType = "PURCHASE_REQUEST";
        /// <summary>Màn hình nghiệp vụ.</summary>
        public const string ScreenId = "scm-pr";
        /// <summary>D-46: cấp 1 – Trưởng bộ phận Mua hàng (mục <c>#/wf-inbox?type=scm-pr-l1</c>).</summary>
        public const string Level1ScreenId = "scm-pr-l1";
        /// <summary>D-46: cấp 2 – Giám đốc vận hành (mục <c>#/wf-inbox?type=scm-pr-l2</c>).</summary>
        public const string Level2ScreenId = "scm-pr-l2";

        public static string Label(string? status) => status switch
        {
            Draft => "Nháp",
            PendingApproval => "Chờ duyệt",
            Approved => "Đã duyệt",
            Rejected => "Từ chối",
            Converted => "Đã lập PO",
            Cancelled => "Hủy",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>T14 – trạng thái kế hoạch sản xuất (STATUS_CATALOG: ProductionPlan.Status, PROPOSED – BA/PO CONFIRM).</summary>
    public static class ProductionPlanStatus
    {
        public const string Draft = "Draft";
        public const string Approved = "Approved";
        public const string Released = "Released";
        public const string Completed = "Completed";

        public static string Label(string? status) => status switch
        {
            Draft => "Nháp",
            Approved => "Đã duyệt",
            Released => "Đã phát hành",
            Completed => "Hoàn thành",
            _ => status ?? string.Empty,
        };
    }
}

namespace AppDelicare.Model.Helper
{
    /// <summary>
    /// T15 – trạng thái đơn mua NCC (STATUS_CATALOG: PurchaseOrder.Status, PROPOSED – BA/PO CONFIRM). Thêm <c>PendingApproval</c> (chờ duyệt 1 cấp
    /// qua engine T12 – giống PurchaseRequest) – GAP-PUR-02.
    /// </summary>
    public static class PurchaseOrderStatus
    {
        public const string Draft = "Draft";
        public const string PendingApproval = "PendingApproval";
        public const string Approved = "Approved";
        public const string Sent = "Sent";
        public const string PartiallyReceived = "PartiallyReceived";
        public const string Received = "Received";
        public const string Closed = "Closed";
        public const string Cancelled = "Cancelled";

        /// <summary>BusinessType của ApprovalRequest khi duyệt PO (handler <c>PurchaseOrderApprovalHandler</c>).</summary>
        public const string BusinessType = "PURCHASE_ORDER";

        /// <summary>Màn hình đơn mua – quyền duyệt = mục menu <c>#/wf-inbox?type=scm-po</c> (D-31).</summary>
        public const string ScreenId = "scm-po";

        /// <summary>
        /// T15.1 (D-11, anh chốt 2026-11-16): PO có tổng ≥ <c>BusinessRules:PurchaseOrderDirectorApprovalAmount</c> chỉ Giám đốc duyệt – quyền = mục menu
        /// <c>#/wf-inbox?type=scm-po-director</c> "Duyệt – Đơn mua hàng (Giám đốc)" (không phải màn hình riêng, chỉ là mục quyền duyệt).
        /// </summary>
        public const string DirectorScreenId = "scm-po-director";

        /// <summary>PO được nhận hàng (lập phiếu nhập) – PROPOSED: Đã duyệt cũng nhận được (Pilot có thể không bấm "Đã gửi NCC").</summary>
        public static readonly IReadOnlyList<string> Receivable = [Approved, Sent, PartiallyReceived];

        public static string Label(string? status) => status switch
        {
            Draft => "Nháp",
            PendingApproval => "Chờ duyệt",
            Approved => "Đã duyệt",
            Sent => "Đã gửi NCC",
            PartiallyReceived => "Nhận một phần",
            Received => "Đã nhận đủ",
            Closed => "Đóng",
            Cancelled => "Hủy",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>
    /// T15 – trạng thái phiếu nhập kho (STATUS_CATALOG: GoodsReceipt.Status, PROPOSED – BA/PO CONFIRM). Thêm <c>Cancelled</c> (hủy phiếu chưa ghi sổ – GAP-PUR-05).
    /// </summary>
    public static class GoodsReceiptStatus
    {
        public const string QcPending = "QCPending";
        public const string Accepted = "Accepted";
        public const string PartiallyAccepted = "PartiallyAccepted";
        public const string Rejected = "Rejected";
        public const string Posted = "Posted";
        public const string Cancelled = "Cancelled";

        /// <summary>Đã kết luận QC, chờ ghi sổ.</summary>
        public static readonly IReadOnlyList<string> Concluded = [Accepted, PartiallyAccepted, Rejected];

        public static string Label(string? status) => status switch
        {
            QcPending => "Chờ QC",
            Accepted => "Đạt",
            PartiallyAccepted => "Đạt một phần",
            Rejected => "Không đạt",
            Posted => "Đã ghi sổ",
            Cancelled => "Hủy",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>
    /// T15 – kết quả QC đầu vào theo BUSINESS_RULES §4 / D-10: DAT / DAT_MOT_PHAN / KHONG_DAT. Nhận cả mã BRD PASS / HOLD / REJECT và quy về
    /// DAT / DAT_MOT_PHAN / KHONG_DAT (ánh xạ BUSINESS_RULES §4 – PROPOSED).
    /// </summary>
    public static class IncomingQcResult
    {
        public const string Dat = LotQcStatus.Dat;
        public const string DatMotPhan = LotQcStatus.DatMotPhan;
        public const string KhongDat = LotQcStatus.KhongDat;

        /// <summary>Chuẩn hóa mã kết quả; null = không hợp lệ.</summary>
        public static string? Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "DAT" or "PASS" => Dat,
            "DAT_MOT_PHAN" or "HOLD" => DatMotPhan,
            "KHONG_DAT" or "REJECT" => KhongDat,
            _ => null,
        };

        public static string Label(string? result) => result switch
        {
            Dat => "Đạt",
            DatMotPhan => "Đạt một phần",
            KhongDat => "Không đạt",
            _ => result ?? string.Empty,
        };
    }

    /// <summary>T15 – loại chứng từ nguồn / sự cố NCC dùng khi nhập hàng (PROPOSED – BA/PO CONFIRM).</summary>
    public static class ReceivingCode
    {
        /// <summary><c>InventoryTransaction.ReferenceType</c> của bút toán nhập từ phiếu nhập.</summary>
        public const string GoodsReceiptReference = "GoodsReceipt";
        /// <summary><c>TemperatureLog.MeasurementType</c> khi nhận hàng (BUSINESS_RULES §4).</summary>
        public const string ReceivingMeasurement = "RECEIVING";
        public const string IssueTypeQuality = "QUALITY";
        public const string IssueTypeTemperature = "TEMPERATURE";
        public const string IssueOpen = "Open";
        public const string SeverityHigh = "High";
        public const string SeverityMedium = "Medium";
        /// <summary>
        /// T15.1: đánh dấu "ưu tiên dùng trước" trong <c>MaterialLot.Note</c> (lô nhận có điều kiện nhiệt độ) và trong <c>QCInspectionLine.Note</c>
        /// – bảng không có cột ưu tiên (GAP-PUR-10).
        /// </summary>
        public const string UseFirstMarker = "[UU_TIEN_DUNG_TRUOC]";
    }

    /// <summary>
    /// T15.1 (anh chốt 2026-11-16) – vùng nhiệt độ nhận hàng theo <c>BusinessRules:ReceivingTemp</c>: ACCEPT (≤ AcceptMax), CONDITIONAL (≤ ConditionalMax:
    /// nhận có điều kiện), REJECT (lớn hơn: chỉ KHONG_DAT), MISSING (hàng mát / đông lạnh chưa đo), NOT_CHECKED (DRY / AMBIENT – không kiểm).
    /// </summary>
    public static class ReceivingTempZone
    {
        public const string Accept = "ACCEPT";
        public const string Conditional = "CONDITIONAL";
        public const string Reject = "REJECT";
        public const string Missing = "MISSING";
        public const string NotChecked = "NOT_CHECKED";

        public static string Label(string? zone) => zone switch
        {
            Accept => "Đạt nhiệt độ",
            Conditional => "Nhận có điều kiện",
            Reject => "Loại (vượt ngưỡng)",
            Missing => "Chưa đo nhiệt độ",
            NotChecked => "Không kiểm nhiệt độ",
            _ => zone ?? string.Empty,
        };
    }
}
