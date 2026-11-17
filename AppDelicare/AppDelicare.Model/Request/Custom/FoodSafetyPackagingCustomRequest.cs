using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>BE2.5 (spec 9.2 GET checklists/{id}/run, Catalog 161): mở 1 lần chạy checklist ATTP theo ngày / ca / mẻ (query).</summary>
    public class ChecklistRunRequest
    {
        /// <summary>Id bộ checklist (FoodSafetyChecklist).</summary>
        [Required]
        public Guid ChecklistId { get; set; }
        /// <summary>Ngày kiểm; trống = hôm nay.</summary>
        public DateTime? CheckDate { get; set; }
        /// <summary>Ca; trống mà có mẻ → ca của lệnh SX của mẻ.</summary>
        public Guid? ShiftId { get; set; }
        public Guid? ProductionBatchId { get; set; }
    }

    /// <summary>
    /// BE2.5 (spec 9.2 POST checklists/{id}/results, Catalog 162): ghi kết quả checklist ATTP – JSON vì có danh sách tiêu chí.
    /// Kết quả = dòng mới trong FoodSafetyChecklistResult (giữ lịch sử, dòng mới nhất của tiêu chí là kết quả hiện tại – D-54 c).
    /// </summary>
    public class ChecklistRecordRequest
    {
        [Required]
        public Guid ChecklistId { get; set; }
        public DateTime? CheckDate { get; set; }
        public Guid? ShiftId { get; set; }
        public Guid? ProductionBatchId { get; set; }
        /// <summary>Trạm bếp (KitchenWorkstation) nơi kiểm.</summary>
        public Guid? WorkstationId { get; set; }
        public List<ChecklistRecordItemRequest> ListItem { get; set; } = [];
    }

    public class ChecklistRecordItemRequest
    {
        /// <summary>Id tiêu chí (FoodSafetyChecklistItem); trống thì dùng <see cref="ItemCode"/>.</summary>
        public Guid? ChecklistItemId { get; set; }
        [MaxLength(50)]
        public string? ItemCode { get; set; }
        /// <summary>PASS | FAIL.</summary>
        [Required]
        [MaxLength(30)]
        public string ResultCode { get; set; } = string.Empty;
        [MaxLength(500)]
        public string? ActualValue { get; set; }
        /// <summary>Bắt buộc khi FAIL.</summary>
        [MaxLength(2000)]
        public string? CorrectiveAction { get; set; }
        [MaxLength(1000)]
        public string? EvidenceUrl { get; set; }
    }

    /// <summary>BE2.5 (spec 9.4 POST cleaning-batches, Catalog 167): lập mẻ vệ sinh khay / hộp từ phiếu kiểm tra thu hồi (form).</summary>
    public class CleaningBatchCreateRequest
    {
        [Required]
        public Guid ReturnInspectionId { get; set; }
        /// <summary>Số khay / hộp đưa vào mẻ; trống = toàn bộ số dùng được còn lại của phiếu kiểm tra.</summary>
        public int? ContainerQuantity { get; set; }
        public Guid? ResponsibleEmployeeId { get; set; }
        /// <summary>Ca (hậu tố số VS-YYMMDD-&lt;CA&gt;); trống = ca theo giờ hiện tại.</summary>
        public Guid? ShiftId { get; set; }
    }

    /// <summary>BE2.5 (spec 9.4 POST cleaning-batches/{id}/results, Catalog 168): ghi kết quả các bước vệ sinh (checklist VS-KHAY) – JSON vì có danh sách.</summary>
    public class CleaningRecordRequest
    {
        /// <summary>Id mẻ vệ sinh.</summary>
        [Required]
        public Guid Id { get; set; }
        /// <summary>Bộ checklist; trống = bộ <c>BusinessRules:CleaningChecklistCode</c>.</summary>
        public Guid? ChecklistId { get; set; }
        public Guid? WorkstationId { get; set; }
        public List<CleaningRecordItemRequest> ListItem { get; set; } = [];
    }

    public class CleaningRecordItemRequest
    {
        public Guid? ChecklistItemId { get; set; }
        [MaxLength(50)]
        public string? ItemCode { get; set; }
        [Required]
        [MaxLength(30)]
        public string ResultCode { get; set; } = string.Empty;
        [MaxLength(500)]
        public string? ActualValue { get; set; }
        [MaxLength(255)]
        public string? ChemicalUsed { get; set; }
        [MaxLength(2000)]
        public string? CorrectiveAction { get; set; }
        [MaxLength(1000)]
        public string? EvidenceUrl { get; set; }
    }

    /// <summary>BE2.5 (spec 9.4 POST cleaning-batches/{id}/complete, Catalog 169): kết luận mẻ vệ sinh (form) – PASS | REJECT, REJECT bắt buộc lý do.</summary>
    public class CleaningCompleteRequest
    {
        [Required]
        public Guid Id { get; set; }
        [Required]
        [MaxLength(50)]
        public string QcResult { get; set; } = string.Empty;
        [MaxLength(2000)]
        public string? Note { get; set; }
    }

    /// <summary>BE2.5 (spec 9.5 transition, Catalog 170): chuyển trạng thái sự cố NCC (form) – Resolved bắt buộc cách xử lý.</summary>
    public class SupplierIssueTransitionRequest
    {
        [Required]
        public Guid Id { get; set; }
        /// <summary>InProgress | Resolved | Closed.</summary>
        [Required]
        [MaxLength(30)]
        public string To { get; set; } = string.Empty;
        [MaxLength(2000)]
        public string? Resolution { get; set; }
        /// <summary>Người phụ trách (User.Id – PROPOSED D-54 e); chuyển Đang xử lý mà trống = người thao tác.</summary>
        public Guid? OwnerId { get; set; }
    }

    /// <summary>BE2.5 (spec 9.5 CRUD list): danh sách sự cố NCC + quá hạn (query).</summary>
    public class SupplierIssueListRequest
    {
        [MaxLength(30)]
        public string? Status { get; set; }
        public Guid? SupplierId { get; set; }
        [MaxLength(30)]
        public string? Severity { get; set; }
        public bool? Overdue { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        [MaxLength(200)]
        public string? SearchText { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    /// <summary>BE2.5 (spec 10.4 POST container-returns, Catalog 194): ghi thu hồi khay / hộp của 1 đơn giao (form).</summary>
    public class ContainerReturnRecordRequest
    {
        [Required]
        public Guid DeliveryOrderId { get; set; }
        public DateTime? ReturnDate { get; set; }
        /// <summary>Số phải thu; trống = số còn ở khách của đơn giao (OUT − IN − LOSS theo số đơn giao).</summary>
        public int? ExpectedQuantity { get; set; }
        [Required]
        public int ReturnedQuantity { get; set; }
        /// <summary>Trong số trả về có bao nhiêu hỏng (≤ số trả về).</summary>
        public int? DamagedQuantity { get; set; }
        /// <summary>Loại bao bì; trống = loại đã giao (OUT) của đơn giao.</summary>
        [MaxLength(100)]
        public string? PackagingType { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>BE2.5 (spec 10.4 POST container-returns/{id}/inspect, Catalog 195): kiểm tra vật dụng thu hồi (form).</summary>
    public class ContainerReturnInspectRequest
    {
        /// <summary>Id phiếu thu hồi (ContainerReturn).</summary>
        [Required]
        public Guid Id { get; set; }
        /// <summary>OK | SHORTAGE | DAMAGED | DIRTY | REJECTED.</summary>
        [Required]
        [MaxLength(50)]
        public string InspectionResult { get; set; } = string.Empty;
        /// <summary>Số hỏng khi kiểm; trống = số hỏng lúc thu hồi.</summary>
        public decimal? DamagedQty { get; set; }
        /// <summary>Số thiếu so với số phải thu; trống = phải thu − trả về (≥ 0).</summary>
        public decimal? MissingQty { get; set; }
        public Guid? InspectorEmployeeId { get; set; }
        /// <summary>Người phụ trách mẻ vệ sinh tự lập.</summary>
        public Guid? ResponsibleEmployeeId { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>BE2.5 (spec 10.4): danh sách phiếu thu hồi theo trạng thái / khách / ngày (query).</summary>
    public class ContainerReturnListRequest
    {
        [MaxLength(50)]
        public string? Status { get; set; }
        public Guid? CustomerId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    /// <summary>BE2.5 (spec 10.4 GET packaging-ledger/balance, Catalog 196): số khay / hộp còn ở khách + tỷ lệ thất thoát (query).</summary>
    public class PackagingBalanceRequest
    {
        public Guid? CustomerId { get; set; }
        [MaxLength(100)]
        public string? PackagingType { get; set; }
        /// <summary>Kỳ tính tỷ lệ thất thoát (Σ LOSS ÷ Σ OUT trong kỳ); số còn ở khách luôn tính lũy kế tới <see cref="To"/>.</summary>
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public bool? OverThresholdOnly { get; set; }
    }

    /// <summary>BE2.5: ghi tay 1 dòng sổ bao bì (tồn đầu kỳ ở khách, thất thoát, điều chỉnh) – form, lý do bắt buộc.</summary>
    public class PackagingRecordRequest
    {
        [Required]
        public Guid CustomerId { get; set; }
        /// <summary>OUT | IN | LOSS.</summary>
        [Required]
        [MaxLength(30)]
        public string TransactionType { get; set; } = string.Empty;
        [Required]
        [MaxLength(100)]
        public string PackagingType { get; set; } = string.Empty;
        [Required]
        public decimal Quantity { get; set; }
        public Guid? DeliveryOrderId { get; set; }
        [MaxLength(30)]
        public string? ConditionCode { get; set; }
        [Required]
        [MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;
    }
}
