using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Model
{
    /// <summary>BIZ-3.2 (D-77 – A7): bảng kê thanh toán theo khách × kỳ – chỉ đơn đối soát đã chốt; tính khi xem, không lưu.</summary>
    public class BillingStatementModel
    {
        public Guid CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public string? CustomerName { get; set; }
        public string? TaxCode { get; set; }
        public string? Address { get; set; }
        public Guid? ContractId { get; set; }
        public string? ContractNumber { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public string? BillingBasisName { get; set; }
        public List<BillingStatementRowModel> ListRow { get; set; } = [];
        public decimal TotalBilledQuantity { get; set; }
        public decimal TotalFaultQuantity { get; set; }
        public decimal MealAmount { get; set; }
        public List<BillingContainerFeeModel> ListContainerFee { get; set; } = [];
        public decimal ContainerFeeAmount { get; set; }
        public decimal SubTotal { get; set; }
        public decimal VatPct { get; set; }
        public decimal VatAmount { get; set; }
        public decimal GrandTotal { get; set; }
        /// <summary>Đơn trong kỳ chưa chốt đối soát – liệt kê riêng, chưa tính tiền.</summary>
        public List<BillingPendingOrderModel> ListPending { get; set; } = [];
        /// <summary>Đơn bù giá 0 (lỗi Delicare – BIZ-3.1) không tính tiền.</summary>
        public int CompensationOrderCount { get; set; }
    }

    public class BillingStatementRowModel
    {
        public DateTime MealDate { get; set; }
        public string? MealType { get; set; }
        public string? MealName { get; set; }
        public string? OrderNumber { get; set; }
        public string? LocationName { get; set; }
        public decimal BilledQuantity { get; set; }
        public decimal FaultQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
    }

    public class BillingContainerFeeModel
    {
        public string PackagingType { get; set; } = string.Empty;
        public decimal PeriodOutQuantity { get; set; }
        public decimal PeriodLossQuantity { get; set; }
        public decimal ThresholdPct { get; set; }
        public decimal OverQuantity { get; set; }
        public decimal FeePerUnit { get; set; }
        public decimal Amount { get; set; }
    }

    public class BillingPendingOrderModel
    {
        public Guid OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public DateTime MealDate { get; set; }
        public string? StatusName { get; set; }
        public decimal OrderedQuantity { get; set; }
        public decimal OrderAmount { get; set; }
    }
}

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>BIZ-3.2: bảng kê thanh toán (query). Trống kỳ = tháng trước.</summary>
    public class BillingStatementRequest
    {
        [Required]
        public Guid CustomerId { get; set; }
        public Guid? ContractId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        /// <summary>xlsx / pdf (xuất / in).</summary>
        public string? Format { get; set; }
    }
}
