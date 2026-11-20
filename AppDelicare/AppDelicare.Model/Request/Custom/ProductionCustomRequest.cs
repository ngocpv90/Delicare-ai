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
    /// BE2.1 (spec 8.1 generate): sinh / làm lại kế hoạch sản xuất Nháp của ngày (KH- theo ngày – GAP-MRP-01 anh chốt) từ đơn Đã xác nhận / Đã chốt,
    /// dòng theo món kèm số phần từng ca (form).
    /// </summary>
    public class ProductionPlanGenerateRequest
    {
        [Required]
        public DateTime ProductionDate { get; set; }
        public Guid? OrganizationUnitId { get; set; }
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
    /// <summary>BOT-3.3 (D-72): nấu thêm – mẻ bổ sung cùng lệnh sản xuất / món với mẻ gốc (thiếu suất, mẻ hỏng…).</summary>
    public class ProductionBatchExtraRequest
    {
        /// <summary>Mẻ gốc (lấy lệnh SX, món, công thức).</summary>
        [Required]
        public Guid Id { get; set; }
        [Range(1, 100000)]
        public decimal Quantity { get; set; }
        [Required, MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

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
        /// <summary>BOT-2.4 (QR <c>start=LINE_&lt;mã&gt;</c>): chỉ mẻ của dây chuyền (ProductionLine.Code của lệnh SX) hoặc bàn nấu (KitchenWorkstation.Code của mẻ) có mã này.</summary>
        [MaxLength(50)]
        public string? LineCode { get; set; }
    }

    /// <summary>BE2.4 (spec 8.2 cancel, Catalog 145): hủy lệnh SX chưa nấu / chưa xuất kho (form) – lý do bắt buộc.</summary>
    public class ProductionOrderCancelRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(1000)]
        public string? Reason { get; set; }
    }

    /// <summary>
    /// BE2.4 (T17 công đoạn theo tổ): bắt đầu / xong 1 công đoạn của mẻ (form). StageCode = mã mục checklist PRD_STAGE (SO_CHE / NAU / CHIA_SUAT…);
    /// TeamId = tổ (OrganizationUnit loại TEAM, trống = tổ mặc định của công đoạn – BusinessRules:ProductionStageTeams); EmployeeIds = người làm (Employee, lặp khóa form).
    /// </summary>
    public class ProductionStageRequest
    {
        [Required]
        public Guid Id { get; set; }
        [Required, MaxLength(50)]
        public string StageCode { get; set; } = string.Empty;
        public Guid? TeamId { get; set; }
        public Guid? KitchenWorkstationId { get; set; }
        public List<Guid>? EmployeeIds { get; set; }
        public DateTime? At { get; set; }
    }

    /// <summary>BE2.4: tiến độ bếp theo tổ / công đoạn của 1 ngày sản xuất.</summary>
    public class ProductionStageBoardRequest
    {
        [Required]
        public DateTime ProductionDate { get; set; }
        public Guid? ShiftId { get; set; }
        public Guid? TeamId { get; set; }
    }

    /// <summary>BE2.4 (spec 8.5 GET actual-costs, Catalog 152): giá thành thực tế theo lệnh SX – lọc theo ngày sản xuất / món / lệnh.</summary>
    public class ActualCostListRequest
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public Guid? DishId { get; set; }
        public Guid? ProductionOrderId { get; set; }
    }

    /// <summary>BE2.4 (spec 7.9, Catalog 135): chênh lệch tiêu hao – lọc theo lệnh SX / ngày sản xuất / nguyên liệu.</summary>
    public class ConsumptionVarianceListRequest
    {
        public Guid? ProductionOrderId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public Guid? IngredientId { get; set; }
        public bool OverThresholdOnly { get; set; }
    }

    /// <summary>BE2.4 (spec 8.5 meal-costs/calculate, Catalog 154): tính giá vốn suất của 1 đơn hàng đã giao / đối soát (form).</summary>
    public class MealCostCalculateRequest
    {
        [Required]
        public Guid OrderId { get; set; }
    }

    /// <summary>BE2.4: giá vốn suất đã tính – theo đơn hoặc theo ngày ăn.</summary>
    public class MealCostListRequest
    {
        public Guid? OrderId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }

    /// <summary>
    /// BE2.4 (spec 7.5 LotExpiryJob – D-45 không job → thao tác tay): lô Khả dụng có hạn dùng &lt; hôm nay → Hết hạn + bút toán WASTE toàn bộ tồn (form).
    /// Preview = true chỉ liệt kê, không ghi.
    /// </summary>
    public class MaterialLotExpireRequest
    {
        public Guid? WarehouseId { get; set; }
        public bool Preview { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }
}
