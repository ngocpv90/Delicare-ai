namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.6 (D-85): tổng quan đăng ký suất tuần – mọi khách có hợp đồng hiệu lực × điểm giao đang hoạt động × 7 ngày.</summary>
    public class OrderWeekOverviewRequest
    {
        /// <summary>Ngày bất kỳ trong tuần (trống = tuần sau). Máy chủ quy về thứ Hai.</summary>
        public DateTime? WeekStart { get; set; }
        public Guid? BranchId { get; set; }
    }

    public class OrderWeekOverviewModel
    {
        public DateTime WeekStart { get; set; }
        public List<DateTime> ListDate { get; set; } = [];
        /// <summary>Mã thực đơn chung đã công bố áp dụng trong tuần.</summary>
        public List<string> ListMenuCode { get; set; } = [];
        public List<OrderWeekOverviewCustomerModel> ListCustomer { get; set; } = [];
        public List<decimal> ListTotal { get; set; } = [];
        public decimal TotalWeek { get; set; }
        public decimal TotalAmount { get; set; }
        public int PointCount { get; set; }
        /// <summary>Điểm có ít nhất 1 đơn (chưa hủy) trong tuần.</summary>
        public int RegisteredPoints { get; set; }
        /// <summary>Điểm chưa có đơn ngày nào (đích của bước "Gửi phiếu").</summary>
        public int UnregisteredPoints { get; set; }
        /// <summary>Điểm có đơn Nháp / Khách gửi chờ xác nhận còn sửa được (đích của bước "Xác nhận").</summary>
        public int PendingPoints { get; set; }
        /// <summary>(Σ suất đã đăng ký − Σ dự báo) / Σ dự báo × 100 trên các ô có cả hai; null = chưa đủ dữ liệu.</summary>
        public decimal? ForecastDeviationPct { get; set; }
        public DateTime NextCutoff { get; set; }
    }

    public class OrderWeekOverviewCustomerModel
    {
        public Guid CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public string? CustomerName { get; set; }
        public List<decimal> ListTotal { get; set; } = [];
        public List<OrderWeekOverviewPointModel> ListPoint { get; set; } = [];
    }

    public class OrderWeekOverviewPointModel
    {
        public Guid DeliveryLocationId { get; set; }
        public string? LocationCode { get; set; }
        public string? LocationName { get; set; }
        public decimal TotalWeek { get; set; }
        /// <summary>NONE (chưa có đơn nào) / DRAFT / SUBMITTED / CONFIRMED / LOCKED / MIXED.</summary>
        public string Status { get; set; } = "NONE";
        public List<OrderWeekOverviewCellModel> ListCell { get; set; } = [];
    }

    /// <summary>Ô ngày: NONE / DRAFT / SUBMITTED (khách gửi – chờ xác nhận) / CONFIRMED / LOCKED (đã chốt trở đi) / NOCONTRACT.</summary>
    public class OrderWeekOverviewCellModel
    {
        public DateTime Date { get; set; }
        public string Status { get; set; } = "NONE";
        public decimal? Quantity { get; set; }
        public decimal? Amount { get; set; }
        public decimal? Forecast { get; set; }
        public Guid? OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public bool AfterCutoff { get; set; }
    }

    /// <summary>UI-6.6: xác nhận nhiều điểm một lần – đơn Nháp / Đã gửi của tuần (chưa quá giờ chốt) → Đã xác nhận.</summary>
    public class OrderWeekConfirmRequest
    {
        public DateTime? WeekStart { get; set; }
        /// <summary>Trống = mọi điểm có đơn chờ xác nhận.</summary>
        public List<Guid> ListDeliveryLocationId { get; set; } = [];
        public Guid? BranchId { get; set; }
    }

    public class OrderWeekConfirmResultModel
    {
        public int Confirmed { get; set; }
        public int Points { get; set; }
        public int Failed { get; set; }
        public List<string> ListError { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }

    /// <summary>UI-6.6: kết quả một phiếu khi tải nhiều phiếu một lần.</summary>
    public class OrderWeekSlipFileResultModel
    {
        public string? FileName { get; set; }
        public bool Ok { get; set; }
        public Guid? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public Guid? DeliveryLocationId { get; set; }
        public string? LocationName { get; set; }
        public int Cells { get; set; }
        public int Created { get; set; }
        public int Updated { get; set; }
        public string? Message { get; set; }
        public List<string> ListError { get; set; } = [];
    }

    public class OrderWeekSlipBatchResultModel
    {
        public int Files { get; set; }
        public int Saved { get; set; }
        public int Failed { get; set; }
        public List<OrderWeekSlipFileResultModel> ListFile { get; set; } = [];
    }
}
