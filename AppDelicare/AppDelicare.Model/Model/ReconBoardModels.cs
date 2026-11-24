namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.8 (D-86 b, c): màn Đối soát – bảng xác nhận ngay trên trang + 2 biểu đồ + 4 chỉ số, theo kỳ / khách.</summary>
    public class ReconBoardRequest
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? BranchId { get; set; }
    }

    public class ReconBoardModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public List<ReconBoardRowModel> ListRow { get; set; } = [];
        public decimal Ordered { get; set; }
        public decimal Delivered { get; set; }
        public decimal Accepted { get; set; }
        public decimal? AcceptedPct { get; set; }
        /// <summary>Σ giá trị chốt (đối soát Đóng).</summary>
        public decimal ClosedAmount { get; set; }
        public int PendingCount { get; set; }
        public int VarianceCount { get; set; }
        public int ClosedCount { get; set; }
        /// <summary>Dòng chờ xác nhận có nhận = đặt, không lỗi – nút "Xác nhận tất cả dòng khớp".</summary>
        public int MatchedPendingCount { get; set; }
        /// <summary>Dòng đã xác nhận (người / bot), chưa chốt – nút "Chốt đối soát".</summary>
        public int ConfirmedOpenCount { get; set; }
        public List<ReconciliationChartPointModel> ListDay { get; set; } = [];
        /// <summary>Chuỗi 5 khâu cả kỳ: Đặt, Sản xuất, Đóng gói, Giao, Nhận.</summary>
        public List<decimal> ListChain { get; set; } = [];
        public List<ReconFaultReasonModel> ListFaultReason { get; set; } = [];
    }

    public class ReconFaultReasonModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Một đơn trong kỳ. Tab: PENDING (chưa người / bot xác nhận), VARIANCE (nhận ≠ đặt hoặc có lỗi Delicare, chưa chốt), CLOSED.
    /// Nguồn số nhận: POD (bàn giao) / BOT / WEB / AUTO (hệ thống tự tạo khi giao xong – chờ xác nhận).
    /// </summary>
    public class ReconBoardRowModel
    {
        public Guid OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public string? OrderStatus { get; set; }
        public DateTime MealDate { get; set; }
        public Guid CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? LocationName { get; set; }
        public decimal Ordered { get; set; }
        public decimal Delivered { get; set; }
        public decimal Accepted { get; set; }
        public decimal? Served { get; set; }
        public decimal? Returned { get; set; }
        public decimal? Wasted { get; set; }
        public decimal? Fault { get; set; }
        public string? FaultReason { get; set; }
        public decimal Billed { get; set; }
        public decimal? Amount { get; set; }
        /// <summary>Giá bình quân / suất của đơn (giá cố định lúc chốt) – để tính lại tiền tại chỗ khi sửa số lỗi.</summary>
        public decimal UnitAmount { get; set; }
        public Guid? ReconciliationId { get; set; }
        public string? ReconciliationStatus { get; set; }
        public string Tab { get; set; } = "PENDING";
        public string Source { get; set; } = "POD";
        public bool Confirmed { get; set; }
        public bool HasPod { get; set; }
        public List<ReconBoardLineModel> ListLine { get; set; } = [];
    }

    public class ReconBoardLineModel
    {
        public Guid OrderLineId { get; set; }
        public string? MealType { get; set; }
        public int? MenuOptionNo { get; set; }
        public decimal Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? Served { get; set; }
    }

    /// <summary>Xác nhận nhiều dòng một lần – mỗi phần tử như <c>OrderReconciliation/Reconcile</c>.</summary>
    public class ReconcileManyRequest
    {
        public List<ReconcileManyItem> ListItem { get; set; } = [];
    }

    public class ReconcileManyItem
    {
        public Guid OrderId { get; set; }
        /// <summary>Trống = số khách nhận (phục vụ mặc định = khách nhận).</summary>
        public decimal? ServedQuantity { get; set; }
        public decimal? ReturnedQuantity { get; set; }
        public decimal? WastedQuantity { get; set; }
        public decimal? DelicareFaultQuantity { get; set; }
        public string? FaultReason { get; set; }
        public string? Remark { get; set; }
        public List<ReconcileManyLine> ListLine { get; set; } = [];
    }

    public class ReconcileManyLine
    {
        public Guid OrderLineId { get; set; }
        public decimal ServedQuantity { get; set; }
    }

    public class ReconCloseManyRequest
    {
        public List<Guid> ListReconciliationId { get; set; } = [];
        public string? Remark { get; set; }
    }

    public class ReconBatchResultModel
    {
        public int Ok { get; set; }
        public int Failed { get; set; }
        public List<ReconBatchErrorModel> ListError { get; set; } = [];
    }

    public class ReconBatchErrorModel
    {
        public Guid Id { get; set; }
        public string? Message { get; set; }
    }
}
