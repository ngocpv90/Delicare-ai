namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.5 (D-84): màn Dự báo theo tuần – khách → điểm giao × buổi × 7 ngày, một số mỗi ô.</summary>
    public class ForecastWeekRequest
    {
        /// <summary>Ngày bất kỳ trong tuần cần xem (trống = tuần sau). Máy chủ quy về thứ Hai.</summary>
        public DateTime? WeekStart { get; set; }
        public Guid? BranchId { get; set; }
    }

    public class ForecastWeekModel
    {
        public DateTime WeekStart { get; set; }
        public List<DateTime> ListDate { get; set; } = [];
        public List<ForecastWeekCustomerModel> ListCustomer { get; set; } = [];
        public List<decimal> ListTotal { get; set; } = [];
        public decimal TotalWeek { get; set; }
        public int DraftCells { get; set; }
        public int MissingCells { get; set; }
        public int PublishedCells { get; set; }
        public int RegisteredCells { get; set; }
        /// <summary>Sai số bình quân 4 tuần qua (MAPE %, null = chưa đủ dữ liệu).</summary>
        public decimal? Mape4Weeks { get; set; }
        public int LookbackWeeks { get; set; }
    }

    public class ForecastWeekCustomerModel
    {
        public Guid CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public string? CustomerName { get; set; }
        public List<decimal> ListTotal { get; set; } = [];
        public List<ForecastWeekRowModel> ListRow { get; set; } = [];
    }

    public class ForecastWeekRowModel
    {
        public Guid? DeliveryLocationId { get; set; }
        public string? DeliveryLocationName { get; set; }
        public string? MealType { get; set; }
        public List<ForecastWeekCellModel> ListCell { get; set; } = [];
    }

    /// <summary>Ô: số sẽ dùng (<see cref="Value"/>) + trạng thái PUBLISHED / DRAFT / REGISTERED / MISSING / LOCKED (ngoài hiệu lực hợp đồng) / EMPTY.</summary>
    public class ForecastWeekCellModel
    {
        public DateTime Date { get; set; }
        public string Status { get; set; } = "EMPTY";
        public decimal? Value { get; set; }
        public decimal? Published { get; set; }
        public decimal? Draft { get; set; }
        public bool DraftEdited { get; set; }
        public decimal? Suggested { get; set; }
        public decimal? Average { get; set; }
        public decimal? Registered { get; set; }
        public List<decimal?> ListHistory { get; set; } = [];
        public List<DateTime> ListHistoryDate { get; set; } = [];
    }

    public class ForecastCellRequest
    {
        public Guid CustomerId { get; set; }
        public Guid? DeliveryLocationId { get; set; }
        public string? MealType { get; set; }
        public DateTime Date { get; set; }
        /// <summary>Số suất; null / &lt; 0 = xóa bản Nháp của ô.</summary>
        public decimal? Quantity { get; set; }
    }

    public class ForecastWeekActionRequest
    {
        public DateTime? WeekStart { get; set; }
        public Guid? CustomerId { get; set; }
    }

    public class ForecastWeekPublishResultModel
    {
        public DateTime WeekStart { get; set; }
        public int Published { get; set; }
        public int SkippedRegistered { get; set; }
        public int Superseded { get; set; }
        public int Missing { get; set; }
        public List<string> ListMissing { get; set; } = [];
    }
}
