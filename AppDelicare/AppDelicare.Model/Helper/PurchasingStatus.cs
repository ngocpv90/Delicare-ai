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
    }
}
