using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.25 (D-106): "Xong mẻ" một chạm – nhiệt độ lõi, lưu mẫu (xác nhận), số suất nấu được, hao hụt trong mẻ, bắt đầu mẻ kế tiếp cùng dây chuyền.</summary>
    public class ProductionBatchCompleteRequest
    {
        [Required]
        public Guid Id { get; set; }
        public decimal? CoreTemperatureC { get; set; }
        [MaxLength(1000)]
        public string? CorrectiveAction { get; set; }
        /// <summary>Người dùng xác nhận đã lưu mẫu món này trong ca (không tích sẵn – hồ sơ ATTP). Món × ca đã có mẫu → bỏ qua.</summary>
        public bool RetainSample { get; set; }
        [MaxLength(500)]
        public string? SampleNote { get; set; }
        /// <summary>Số suất nấu được – trống = số kế hoạch của mẻ.</summary>
        public decimal? ProducedQuantity { get; set; }
        public List<ProductionBatchCompleteWaste> ListWaste { get; set; } = [];
        public Guid? StartNextBatchId { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    public class ProductionBatchCompleteWaste
    {
        [MaxLength(50)]
        public string? WasteType { get; set; }
        public decimal? QuantityMeals { get; set; }
        public decimal? QuantityKg { get; set; }
        [MaxLength(500)]
        public string? Reason { get; set; }
    }

    public class ProductionBatchStartManyRequest
    {
        public List<Guid> ListId { get; set; } = [];
    }

    public class ProductionBatchStartManyModel
    {
        public List<Guid> ListStarted { get; set; } = [];
        public List<ProductionBatchStartFailModel> ListFailed { get; set; } = [];
    }

    public class ProductionBatchStartFailModel
    {
        public Guid Id { get; set; }
        public string? Message { get; set; }
    }
}
