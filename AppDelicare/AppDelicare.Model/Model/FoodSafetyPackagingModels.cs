using AppDelicare.Model.Entity;

namespace AppDelicare.Model.Model
{
    /// <summary>BE2.5 (spec 9.2 run, Catalog 161): 1 lần chạy checklist ATTP theo ngày / ca / mẻ – tiêu chí theo SortOrder ghép kết quả hiện tại + tỷ lệ đạt 7 ngày.</summary>
    public class ChecklistRunModel
    {
        public FoodSafetyChecklist Checklist { get; set; } = null!;
        public DateTime CheckDate { get; set; }
        public Guid? ShiftId { get; set; }
        public string? ShiftCode { get; set; }
        public string? ShiftName { get; set; }
        public Guid? ProductionBatchId { get; set; }
        public string? BatchNumber { get; set; }
        /// <summary>Hạn làm checklist = giờ xuất bếp của ngày / ca + <c>ChecklistOffsetsMinutes[Code]</c> (BUSINESS_RULES §6); null khi bộ không có độ lệch / không có ca.</summary>
        public DateTime? DueAt { get; set; }
        public string? DueSource { get; set; }
        /// <summary>Quá hạn mà còn tiêu chí bắt buộc chưa có kết quả (cờ khi xem – D-45 không job).</summary>
        public bool IsOverdue { get; set; }
        public List<ChecklistRunItemModel> ListItem { get; set; } = [];
        public int RequiredCount { get; set; }
        /// <summary>Số tiêu chí đã có kết quả (mới nhất).</summary>
        public int DoneCount { get; set; }
        public int PassCount { get; set; }
        public int FailCount { get; set; }
        /// <summary>Mọi tiêu chí bắt buộc đã có kết quả.</summary>
        public bool IsComplete { get; set; }
        /// <summary>Tiêu chí trọng yếu đang KHÔNG ĐẠT (kết quả mới nhất).</summary>
        public List<string> ListCriticalFailure { get; set; } = [];
        /// <summary>Tỷ lệ đạt 7 ngày tới ngày kiểm (cùng bộ checklist, cùng ca nếu có ca) – spec 9.2 passRate7d.</summary>
        public List<ChecklistPassRateModel> ListPassRate7d { get; set; } = [];
    }

    public class ChecklistRunItemModel
    {
        public FoodSafetyChecklistItem Item { get; set; } = null!;
        /// <summary>Kết quả hiện tại (dòng mới nhất của tiêu chí trong lần chạy); null = chưa kiểm.</summary>
        public FoodSafetyChecklistResult? Result { get; set; }
        public string? ResultName { get; set; }
        public string? CheckedByName { get; set; }
        /// <summary>Số lần đã ghi (lịch sử – lần kiểm lại sau khắc phục).</summary>
        public int HistoryCount { get; set; }
    }

    public class ChecklistPassRateModel
    {
        public DateTime Date { get; set; }
        public int Total { get; set; }
        public int Pass { get; set; }
        /// <summary>% đạt (2 số lẻ); null khi ngày không có kết quả.</summary>
        public decimal? Rate { get; set; }
    }

    /// <summary>BE2.5 (spec 9.2 results): kết quả ghi checklist – số dòng ghi, lỗi trọng yếu, lần chạy sau khi ghi.</summary>
    public class ChecklistRecordModel
    {
        /// <summary>UI-6.30: mẻ đã bị giữ do lỗi trọng yếu ("số mẻ – tác động").</summary>
        public List<string> ListHeldBatch { get; set; } = [];
        public int Saved { get; set; }
        public List<ChecklistCriticalFailureModel> ListCriticalFailure { get; set; } = [];
        public ChecklistRunModel Run { get; set; } = null!;
        public List<string> ListWarning { get; set; } = [];
    }

    public class ChecklistCriticalFailureModel
    {
        public Guid ChecklistItemId { get; set; }
        public string? ItemCode { get; set; }
        public string? ItemName { get; set; }
        public string? ActualValue { get; set; }
        public string? CorrectiveAction { get; set; }
        public Guid? ProductionBatchId { get; set; }
        public string? BatchNumber { get; set; }
        /// <summary>Tác động lên mẻ: trạng thái mẻ trước → sau / QcStatus (vd. "Đạt → Chờ QC, tạm giữ").</summary>
        public string? BatchAction { get; set; }
    }

    /// <summary>BE2.5 (spec 9.4): mẻ vệ sinh khay / hộp + 6 bước + nguồn (phiếu thu hồi / kiểm tra).</summary>
    public class CleaningBatchDetailModel
    {
        /// <summary>UI-6.32: hóa chất khử khuẩn gợi ý (lần ghi gần nhất, ưu tiên cùng loại bao bì).</summary>
        public string? SuggestedChemical { get; set; }
        public CleaningBatch Batch { get; set; } = null!;
        public string? StatusName { get; set; }
        public string? QcResultName { get; set; }
        public string? PackagingType { get; set; }
        public string? ResponsibleName { get; set; }
        public ReturnInspection? Inspection { get; set; }
        public ContainerReturn? ContainerReturn { get; set; }
        public string? CustomerName { get; set; }
        public string? DeliveryNumber { get; set; }
        public Guid? ChecklistId { get; set; }
        public string? ChecklistCode { get; set; }
        public List<CleaningStepModel> ListStep { get; set; } = [];
        public int StepCount { get; set; }
        public int PassedStepCount { get; set; }
        /// <summary>Mọi bước bắt buộc ĐẠT – kết luận PASS được.</summary>
        public bool CanPass { get; set; }
        /// <summary>recordResults, complete (theo trạng thái).</summary>
        public List<string> AllowedActions { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }

    public class CleaningStepModel
    {
        public FoodSafetyChecklistItem Item { get; set; } = null!;
        public CleaningChecklistResult? Result { get; set; }
        public string? ResultName { get; set; }
        public string? CheckedByName { get; set; }
        public int HistoryCount { get; set; }
        public bool RequiresChemical { get; set; }
    }

    /// <summary>BE2.5 (spec 10.4): phiếu thu hồi khay / hộp + kiểm tra + mẻ vệ sinh + sổ bao bì của đơn giao.</summary>
    public class ContainerReturnDetailModel
    {
        public ContainerReturn Return { get; set; } = null!;
        public string? StatusName { get; set; }
        public string? CustomerName { get; set; }
        public string? DeliveryNumber { get; set; }
        public string? PackagingType { get; set; }
        /// <summary>Số khay của đơn giao còn ở khách (OUT − IN − LOSS theo số đơn giao).</summary>
        public decimal OutstandingOfDelivery { get; set; }
        /// <summary>Số khay loại này của khách còn ở khách (mọi đơn giao).</summary>
        public decimal OutstandingOfCustomer { get; set; }
        public List<ReturnInspection> ListInspection { get; set; } = [];
        public List<CleaningBatch> ListCleaningBatch { get; set; } = [];
        public List<PackagingLedger> ListLedger { get; set; } = [];
        /// <summary>record (Chờ thu hồi), inspect (Đã thu hồi), delete (Chờ thu hồi).</summary>
        public List<string> AllowedActions { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }

    public class ContainerReturnListItemModel
    {
        public ContainerReturn Return { get; set; } = null!;
        public string? StatusName { get; set; }
        public string? CustomerName { get; set; }
        public string? DeliveryNumber { get; set; }
        public string? PackagingType { get; set; }
        public string? InspectionResult { get; set; }
        public string? InspectionResultName { get; set; }
    }

    public class ContainerReturnListModel
    {
        public List<ContainerReturnListItemModel> ListItem { get; set; } = [];
        public int TotalRecords { get; set; }
        public Dictionary<string, int> StatusCounts { get; set; } = [];
    }

    /// <summary>BE2.5 (spec 10.4 balance, Catalog 196): số khay / hộp còn ở khách theo (khách, loại) + tỷ lệ thất thoát so với ngưỡng.</summary>
    public class PackagingBalanceItemModel
    {
        /// <summary>UI-6.35 (D-116): khay của phiếu Chờ thu hồi quá ContainerReturnDueDays ngày.</summary>
        public decimal OverdueQuantity { get; set; }
        public Guid CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public string? CustomerName { get; set; }
        public string PackagingType { get; set; } = string.Empty;
        /// <summary>Lũy kế tới ngày "đến".</summary>
        public decimal OutQuantity { get; set; }
        public decimal InQuantity { get; set; }
        /// <summary>Trong số khách trả có bao nhiêu hỏng (ConditionCode DAMAGED).</summary>
        public decimal DamagedInQuantity { get; set; }
        public decimal LossQuantity { get; set; }
        /// <summary>Còn ở khách = OUT − IN − LOSS.</summary>
        public decimal Outstanding { get; set; }
        /// <summary>Trong kỳ (từ – đến): giao / thất thoát.</summary>
        public decimal PeriodOutQuantity { get; set; }
        public decimal PeriodLossQuantity { get; set; }
        /// <summary>Tỷ lệ thất thoát % = thất thoát trong kỳ ÷ giao trong kỳ × 100 (2 số lẻ); null khi kỳ không có giao.</summary>
        public decimal? LossRatePct { get; set; }
        /// <summary>Ngưỡng của khách (<c>Customer.ExternalData</c> containerLossMaxPct – GAP-PKG-01) hoặc <c>KpiContainerLossMaxPct</c>.</summary>
        public decimal LossThresholdPct { get; set; }
        public bool IsOverThreshold { get; set; }
    }

    public class PackagingBalanceModel
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public List<PackagingBalanceItemModel> ListItem { get; set; } = [];
        public decimal TotalOutstanding { get; set; }
        public int OverThresholdCount { get; set; }
        /// <summary>UI-6.35: tổng khay quá hạn thu.</summary>
        public decimal OverdueTotal { get; set; }
    }

    /// <summary>BE2.5: dòng sổ bao bì ghi tay + số dư sau khi ghi.</summary>
    public class PackagingRecordModel
    {
        public PackagingLedger Ledger { get; set; } = null!;
        public PackagingBalanceItemModel? Balance { get; set; }
        public List<string> ListWarning { get; set; } = [];
    }

    /// <summary>BE2.5 (spec 9.5): sự cố NCC + tên + quá hạn + chuyển trạng thái được phép.</summary>
    public class SupplierIssueListItemModel
    {
        /// <summary>UI-6.33 (D-114): tên loại, hướng xử lý chuẩn tách từ Resolution "[MÃ] ghi chú".</summary>
        public string? IssueTypeName { get; set; }
        public string? ResolutionTypeCode { get; set; }
        public string? ResolutionTypeName { get; set; }
        public string? ResolutionNote { get; set; }
        public SupplierIssue Issue { get; set; } = null!;
        public string? StatusName { get; set; }
        public string? SeverityName { get; set; }
        public string? SupplierName { get; set; }
        public string? GoodsReceiptNumber { get; set; }
        public string? PurchaseOrderNumber { get; set; }
        public string? OwnerName { get; set; }
        /// <summary>DueAt &lt; bây giờ và chưa Đã xử lý / Đóng (cờ khi xem – thay job hằng giờ, D-45).</summary>
        public bool IsOverdue { get; set; }
        public decimal? HoursToDue { get; set; }
        public List<string> AllowedTransitions { get; set; } = [];
    }

    public class SupplierIssueListModel
    {
        public List<SupplierIssueListItemModel> ListItem { get; set; } = [];
        public int TotalRecords { get; set; }
        public Dictionary<string, int> StatusCounts { get; set; } = [];
        public int OverdueCount { get; set; }
    }
}
