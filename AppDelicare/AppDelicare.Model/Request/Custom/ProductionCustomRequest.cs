using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>T17: thao tác trạng thái 1 chứng từ sản xuất (form): Id + ghi chú / lý do.</summary>
    public class ProductionActionRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>
    /// T17 (spec 8.1 release, Pilot P7): phát hành kế hoạch Đã duyệt → lệnh SX (PH-) theo món + mẻ (ME-yyMMdd-&lt;CA&gt;-NN), lập lịch ngược từ giờ xuất bếp.
    /// T17.1: kế hoạch ngày chia theo ca của đơn Đã chốt – ShiftId = chỉ phát hành ca đó, bỏ trống = mọi ca chưa phát hành;
    /// ProductionLineId = dây chuyền chia mẻ (năng lực / phút mỗi mẻ);
    /// DispatchTime ("HH:mm") = giờ xuất bếp nhập tay, chỉ khi phát hành 1 ca (bỏ trống = giờ giao sớm nhất của điểm giao thuộc ca − TransportMinutesBeforeCutoff).
    /// </summary>
    public class ProductionPlanReleaseRequest
    {
        [Required]
        public Guid Id { get; set; }
        public Guid? ShiftId { get; set; }
        public Guid? ProductionLineId { get; set; }
        [MaxLength(5)]
        public string? DispatchTime { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>T17: bắt đầu nấu mẻ (form) – trạm bếp (tùy chọn).</summary>
    public class ProductionBatchStartRequest
    {
        [Required]
        public Guid Id { get; set; }
        public Guid? KitchenWorkstationId { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>T17: nấu xong mẻ (form) – số phần thực nấu (tùy chọn, ghi ActualQuantity).</summary>
    public class ProductionBatchFinishRequest
    {
        [Required]
        public Guid Id { get; set; }
        public decimal? ActualQuantity { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>T17: lập (và ghi sổ) phiếu xuất nguyên liệu cho mẻ theo định lượng × số phần kế hoạch – lô tự chọn FEFO / FIFO (T16).</summary>
    public class ProductionBatchIssueRequest
    {
        [Required]
        public Guid Id { get; set; }
        [Required]
        public Guid WarehouseId { get; set; }
        /// <summary>true = ghi sổ ngay (Draft → Issued); false = để Nháp, ghi sổ bằng MaterialIssue/Post.</summary>
        public bool Post { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>T17: ghi nhiệt độ lõi mẻ (TemperatureLog CORE – sổ chỉ ghi thêm). Dưới ngưỡng → bắt buộc hành động khắc phục.</summary>
    public class ProductionBatchTemperatureRequest
    {
        [Required]
        public Guid Id { get; set; }
        public decimal TemperatureC { get; set; }
        public DateTime? MeasuredAt { get; set; }
        [MaxLength(100)]
        public string? DeviceCode { get; set; }
        [MaxLength(1000)]
        public string? CorrectiveAction { get; set; }
        [MaxLength(1000)]
        public string? EvidenceUrl { get; set; }
    }

    /// <summary>T17: ghi lưu mẫu thức ăn của mẻ (FoodSafetyChecklistResult, mục SAMPLE_RETAINED).</summary>
    public class ProductionBatchSampleRequest
    {
        [Required]
        public Guid Id { get; set; }
        public DateTime? CheckedAt { get; set; }
        /// <summary>Mô tả mẫu (vị trí tủ / khối lượng…) – ghi vào ActualValue.</summary>
        [MaxLength(500)]
        public string? ActualValue { get; set; }
        [MaxLength(1000)]
        public string? EvidenceUrl { get; set; }
    }

    /// <summary>T17: ghi sản lượng mẻ (ProductionOutput – 1 dòng / mẻ, ghi lại được tới khi đóng gói).</summary>
    public class ProductionBatchOutputRequest
    {
        [Required]
        public Guid Id { get; set; }
        public decimal ProducedQuantity { get; set; }
        public decimal? RejectedQuantity { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>T17: ghi hao hụt / suất thừa của mẻ (MealWasteRecord – thêm dòng).</summary>
    public class ProductionBatchWasteRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(50)]
        public string? WasteType { get; set; }
        public decimal? QuantityMeals { get; set; }
        public decimal? QuantityKg { get; set; }
        [MaxLength(1000)]
        public string? Reason { get; set; }
        [MaxLength(500)]
        public string? Disposition { get; set; }
    }

    /// <summary>T17 (spec 8.3 batch-board): bảng mẻ của 1 ngày sản xuất.</summary>
    public class ProductionBoardRequest
    {
        [Required]
        public DateTime ProductionDate { get; set; }
        public Guid? ShiftId { get; set; }
        public string? Status { get; set; }
    }
}
