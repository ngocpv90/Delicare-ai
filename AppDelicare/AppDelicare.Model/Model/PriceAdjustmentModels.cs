using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.4 (D-83): tạo phụ lục điều chỉnh giá (một hoặc nhiều loại suất của cùng hợp đồng, cùng thời gian, cùng lý do).</summary>
    public class PriceAdjustmentCreateRequest
    {
        [Required]
        public Guid ContractId { get; set; }
        public List<PriceAdjustmentLineRequest> ListLine { get; set; } = [];
        [Required]
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        [MaxLength(500)]
        public string? Reason { get; set; }
    }

    public class PriceAdjustmentLineRequest
    {
        [MaxLength(50)]
        public string? MealType { get; set; }
        public decimal UnitPrice { get; set; }
    }

    /// <summary>Tab "Loại suất &amp; đơn giá" của hợp đồng: giá gốc + giá đang áp hôm nay + lịch sử điều chỉnh.</summary>
    public class ContractPricesModel
    {
        public Guid ContractId { get; set; }
        public string? ContractStatus { get; set; }
        public bool CanAdjust { get; set; }
        public DateTime MinEffectiveFrom { get; set; }
        public DateTime? ContractEffectiveTo { get; set; }
        public List<ContractPriceRowModel> ListMeal { get; set; } = [];
        public List<PriceAdjustmentRowModel> ListAdjustment { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }

    public class ContractPriceRowModel
    {
        public string? MealType { get; set; }
        public string? MealName { get; set; }
        public decimal BasePrice { get; set; }
        public decimal TodayPrice { get; set; }
        /// <summary>"Giá gốc" hoặc mã phụ lục đang áp.</summary>
        public string? TodaySource { get; set; }
    }

    public class PriceAdjustmentRowModel
    {
        public Guid Id { get; set; }
        public string? Code { get; set; }
        public string? MealType { get; set; }
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public decimal? ChangePct { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public string? Reason { get; set; }
        public int FileCount { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public bool AppliesToday { get; set; }
        public Guid? ApprovalRequestId { get; set; }
    }
}
