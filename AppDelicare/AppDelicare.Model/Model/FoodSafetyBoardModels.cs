namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.30 (D-111): ma trận "Hôm nay" Bộ kiểm tra × Ca (FoodSafetyChecklistResult/GetDayBoard).</summary>
    public class ChecklistDayBoardModel
    {
        public DateTime Date { get; set; }
        public Guid? CurrentShiftId { get; set; }
        public int DueCount { get; set; }
        public int DoneCount { get; set; }
        public int OverdueCount { get; set; }
        public int OpenCriticalCount { get; set; }
        public List<ChecklistBoardShiftModel> ListShift { get; set; } = [];
        public List<ChecklistBoardRowModel> ListChecklist { get; set; } = [];
    }

    public class ChecklistBoardShiftModel
    {
        public Guid Id { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public bool IsCurrent { get; set; }
        /// <summary>Mẻ của ca trong ngày còn có thể bị ảnh hưởng (Kế hoạch / Đang nấu / Chờ QC / Đạt / Đã đóng gói).</summary>
        public List<ChecklistBoardBatchModel> ListBatch { get; set; } = [];
    }

    public class ChecklistBoardBatchModel
    {
        public Guid Id { get; set; }
        public string? BatchNumber { get; set; }
        public string? DishName { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        /// <summary>Đang nấu / Chờ QC – gợi ý tick sẵn khi có lỗi trọng yếu.</summary>
        public bool Suggested { get; set; }
    }

    public class ChecklistBoardRowModel
    {
        public Guid ChecklistId { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public int ItemCount { get; set; }
        public int RequiredCount { get; set; }
        public List<ChecklistBoardCellModel> ListCell { get; set; } = [];
    }

    public class ChecklistBoardCellModel
    {
        public Guid ShiftId { get; set; }
        public int Total { get; set; }
        public int Done { get; set; }
        public int Pass { get; set; }
        public int Fail { get; set; }
        public int CriticalFail { get; set; }
        public DateTime? DueAt { get; set; }
        public string? DueSource { get; set; }
        /// <summary>NOT_DUE | DUE | OVERDUE | PASS | FAIL | CRITICAL.</summary>
        public string Status { get; set; } = "DUE";
        public List<string> ListHeldBatch { get; set; } = [];
    }

    /// <summary>UI-6.30: Sổ kiểm tra (kiểm thực bước 2) – bộ lọc.</summary>
    public class ChecklistBookRequest
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string? Keyword { get; set; }
        public Guid? ChecklistId { get; set; }
        public Guid? ShiftId { get; set; }
        /// <summary>PASS | FAIL.</summary>
        public string? Result { get; set; }
        public bool OnlyCritical { get; set; }
    }

    public class ChecklistBookModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int Runs { get; set; }
        public int Lines { get; set; }
        public int Fail { get; set; }
        public int CriticalFail { get; set; }
        public List<ChecklistBookDayModel> ListDay { get; set; } = [];
    }

    public class ChecklistBookDayModel
    {
        public DateTime Date { get; set; }
        public int Runs { get; set; }
        public int Lines { get; set; }
        public int Fail { get; set; }
        public List<ChecklistBookRowModel> ListRow { get; set; } = [];
    }

    public class ChecklistBookRowModel
    {
        public Guid Id { get; set; }
        public DateTime CheckDate { get; set; }
        public DateTime CheckedAt { get; set; }
        public Guid? ShiftId { get; set; }
        public string? ShiftName { get; set; }
        public Guid ChecklistId { get; set; }
        public string? ChecklistName { get; set; }
        public string? ItemName { get; set; }
        public bool IsCritical { get; set; }
        public string? ExpectedValue { get; set; }
        public string? ActualValue { get; set; }
        public string? ResultCode { get; set; }
        public string? ResultName { get; set; }
        public string? CorrectiveAction { get; set; }
        public List<string> ListBatch { get; set; } = [];
        public string? CheckedByName { get; set; }
        public bool IsRecheck { get; set; }
    }

    /// <summary>UI-6.30: tỷ lệ đạt theo bộ × ngày + tiêu chí hay Không đạt nhất.</summary>
    public class ChecklistPassRateReportModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public List<DateTime> ListDate { get; set; } = [];
        public List<ChecklistPassRateSeriesModel> ListSeries { get; set; } = [];
        public List<ChecklistTopFailModel> ListTopFail { get; set; } = [];
    }

    public class ChecklistPassRateSeriesModel
    {
        public Guid ChecklistId { get; set; }
        public string? Name { get; set; }
        public decimal? RatePct { get; set; }
        public List<decimal?> ListRate { get; set; } = [];
    }

    public class ChecklistTopFailModel
    {
        public Guid ChecklistItemId { get; set; }
        public string? ItemName { get; set; }
        public string? ChecklistName { get; set; }
        public bool IsCritical { get; set; }
        public int FailCount { get; set; }
        public DateTime LastDate { get; set; }
    }
}
