namespace AppDelicare.Model.Model
{
    /// <summary>
    /// UI-6 (D-80): dữ liệu trang chi tiết toàn màn (…/GetOverview) – gộp từ dữ liệu có sẵn, không bảng / cột mới.
    /// Quy ước chung: <c>Detail</c> = model chi tiết đang có của chứng từ (dùng lại, không viết lại luật), cộng phần bổ sung cho phần đầu / chỉ số /
    /// tab Tổng quan; <c>ApprovalRequestId</c> = yêu cầu duyệt mới nhất (tab Duyệt – khuôn tóm tắt APR-UI-1).
    /// </summary>
    public class StockCountOverviewModel
    {
        public StockCountDetailModel Detail { get; set; } = new();
        public Guid? ApprovalRequestId { get; set; }
        public string? ResponsibleName { get; set; }
        public int LineCount { get; set; }
        public int VarianceLineCount { get; set; }
        public int OverThresholdCount { get; set; }
    }
}
