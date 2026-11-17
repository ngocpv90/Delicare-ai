namespace AppDelicare.Model.Helper
{
    /// <summary>T17 – trạng thái lệnh sản xuất (STATUS_CATALOG: ProductionOrder.Status, PROPOSED – BA/PO CONFIRM).</summary>
    public static class ProductionOrderStatus
    {
        public const string Planned = "Planned";
        public const string Released = "Released";
        public const string InProgress = "InProgress";
        public const string Completed = "Completed";
        public const string Closed = "Closed";
        public const string Cancelled = "Cancelled";

        public static string Label(string? status) => status switch
        {
            Planned => "Kế hoạch",
            Released => "Phát hành",
            InProgress => "Đang SX",
            Completed => "Hoàn thành",
            Closed => "Đóng",
            Cancelled => "Hủy",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>
    /// T17 – trạng thái mẻ nấu (STATUS_CATALOG: ProductionBatche.Status, PROPOSED – BA/PO CONFIRM):
    /// Planned → Cooking (Start) → QCPending (Finish) → Passed (Release) / Failed (Reject) → Packed (Pack).
    /// </summary>
    public static class ProductionBatchStatus
    {
        public const string Planned = "Planned";
        public const string Cooking = "Cooking";
        public const string QcPending = "QCPending";
        public const string Passed = "Passed";
        public const string Failed = "Failed";
        public const string Packed = "Packed";
        /// <summary>BE2.4 (spec 8.2 cancel "mẻ → hủy"): mẻ của lệnh SX bị hủy (chưa nấu). STATUS_CATALOG chưa có giá trị này – PROPOSED (D-53).</summary>
        public const string Cancelled = "Cancelled";

        /// <summary>Mẻ đã kết thúc (không còn thao tác sản xuất).</summary>
        public static readonly string[] Closed = [Packed, Failed];

        public static string Label(string? status) => status switch
        {
            Planned => "Kế hoạch",
            Cooking => "Đang nấu",
            QcPending => "Chờ QC",
            Passed => "Đạt",
            Failed => "Không đạt",
            Packed => "Đã đóng gói",
            Cancelled => "Hủy",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>T17 – kết quả QC mẻ (STATUS_CATALOG: ProductionBatche.QcStatus – theo nguồn, không đổi).</summary>
    public static class BatchQcStatus
    {
        public const string Pass = "PASS";
        public const string Hold = "HOLD";
        public const string Reject = "REJECT";

        public static string Label(string? status) => status switch
        {
            Pass => "Đạt",
            Hold => "Tạm giữ",
            Reject => "Từ chối",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>T17 – mã dùng chung của sản xuất (BUSINESS_RULES §6).</summary>
    public static class ProductionCode
    {
        /// <summary>TemperatureLog.MeasurementType của nhiệt độ lõi (BUSINESS_RULES §6; spec 8.3 ghi CORE_TEMP – theo BUSINESS_RULES, GAP-PRD-05).</summary>
        public const string CoreMeasurement = "CORE";
        /// <summary>FoodSafetyChecklistItem.ItemCode của lưu mẫu thức ăn (BUSINESS_RULES §6, seed <c>DB/seed/FoodSafetyChecklist_seed.sql</c>).</summary>
        public const string SampleRetainedItem = "SAMPLE_RETAINED";
        /// <summary>FoodSafetyChecklist.Code của bộ checklist kiểm soát mẻ / thành phẩm (seed, PROPOSED).</summary>
        public const string BatchChecklist = "PRD_BATCH";
        public const string ResultPass = "PASS";
        /// <summary>MealWasteRecord.WasteType khi QC từ chối mẻ (ReferenceLists WasteType).</summary>
        public const string WasteQcReject = "QC_REJECT";
        public const string WasteOverProduction = "OVERPRODUCTION";
        /// <summary>Đánh dấu mẻ trong MaterialIssue.Note (MaterialIssue không có cột ProductionBatchId – GAP-PRD-04).</summary>
        public const string IssueBatchTag = "Mẻ ";

        /// <summary>BE2.4: FoodSafetyChecklist.Code của bộ "Công đoạn sản xuất theo tổ" (seed <c>DB/seed/ProductionStage_seed.sql</c>; mục = công đoạn, SortOrder = thứ tự).</summary>
        public const string StageChecklist = "PRD_STAGE";
        /// <summary>Công đoạn Nấu – bắt đầu Nấu thì mẻ Kế hoạch tự chuyển Đang nấu; nấu xong mẻ (Finish) cần công đoạn này đã xong (nếu mẻ ghi công đoạn).</summary>
        public const string StageCook = "NAU";
        /// <summary>Công đoạn Chia suất – chỉ bắt đầu khi mẻ Đạt QC (nhiệt độ lõi ≥ 70 °C); đóng gói mẻ cần công đoạn này đã xong (nếu mẻ ghi công đoạn).</summary>
        public const string StagePortion = "CHIA_SUAT";
        /// <summary>FoodSafetyChecklistResult.ResultCode của sự kiện công đoạn (sổ ghi thêm: 1 dòng bắt đầu + 1 dòng xong).</summary>
        public const string StageStarted = "STARTED";
        public const string StageDone = "DONE";
    }

    /// <summary>BE2.4 – trạng thái công đoạn của mẻ (tính từ sự kiện FoodSafetyChecklistResult, không lưu cột).</summary>
    public static class ProductionStageStatus
    {
        public const string NotStarted = "NotStarted";
        public const string InProgress = "InProgress";
        public const string Done = "Done";

        public static string Label(string? status) => status switch
        {
            NotStarted => "Chưa làm",
            InProgress => "Đang làm",
            Done => "Đã xong",
            _ => status ?? string.Empty,
        };
    }
}
