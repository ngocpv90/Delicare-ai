namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.9 (D-87): bảng kê tháng – mọi khách một bảng.</summary>
    public class BillingMonthRequest
    {
        /// <summary>Ngày bất kỳ trong tháng (trống = tháng trước).</summary>
        public DateTime? Month { get; set; }
        public Guid? BranchId { get; set; }
    }

    public class BillingMonthModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public List<BillingMonthCustomerModel> ListCustomer { get; set; } = [];
        public decimal GrandTotal { get; set; }
        public decimal MealAmount { get; set; }
        public decimal ContainerFee { get; set; }
        public decimal VatAmount { get; set; }
        public decimal BilledQuantity { get; set; }
        public int ReadyCount { get; set; }
        /// <summary>Khách có phát sinh (có dòng tính tiền hoặc đơn chờ đối soát).</summary>
        public int ActiveCount { get; set; }
        public int PendingOrders { get; set; }
        public int PendingCustomers { get; set; }
        public decimal DeviationPct { get; set; }
        public List<BillingPendingOrderGroupModel> ListPendingGroup { get; set; } = [];
    }

    /// <summary>Trạng thái: READY (đã chốt hết đối soát) / PENDING (còn N đơn chờ – số tạm tính) / NONE (không phát sinh).</summary>
    public class BillingMonthCustomerModel
    {
        public Guid CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public string? CustomerName { get; set; }
        public string? ContractNumber { get; set; }
        public decimal BilledQuantity { get; set; }
        public decimal FaultQuantity { get; set; }
        public decimal MealAmount { get; set; }
        public decimal ContainerFee { get; set; }
        public decimal VatAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public int PendingOrders { get; set; }
        public int CompensationOrders { get; set; }
        public string Status { get; set; } = "NONE";
        public decimal? PreviousMealAmount { get; set; }
        public decimal? ChangePct { get; set; }
        public bool Warn { get; set; }
    }

    public class BillingPendingOrderGroupModel
    {
        public Guid CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public List<BillingPendingOrderModel> ListOrder { get; set; } = [];
    }

    /// <summary>Xuất lô: Excel = 1 tệp nhiều sheet (+ Tổng hợp); PDF = 1 tệp nén, mỗi khách 1 PDF.</summary>
    public class BillingBatchRequest
    {
        public DateTime? Month { get; set; }
        public List<Guid> ListCustomerId { get; set; } = [];
        /// <summary>xlsx / pdf.</summary>
        public string? Format { get; set; }
    }

    public class BillingExportLogModel
    {
        public DateTime At { get; set; }
        public string? ByName { get; set; }
        public string? CustomerName { get; set; }
        public string? Action { get; set; }
        public string? Detail { get; set; }
    }
}
