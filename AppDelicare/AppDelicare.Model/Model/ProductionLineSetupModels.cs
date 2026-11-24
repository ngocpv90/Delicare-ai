using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.26 (D-107): màn "Dây chuyền & trạm bếp" – dây chuyền + nhóm món đang gán + số món; nhóm món chưa gán (món nấu 1 mẻ).</summary>
    public class ProductionLineOverviewModel
    {
        public List<ProductionLineSetupModel> ListLine { get; set; } = [];
        /// <summary>Mọi nhóm món đang dùng (kể cả đã gán) – bảng Gán nhanh.</summary>
        public List<DishCategoryLineModel> ListCategory { get; set; } = [];
        public int UnassignedCategoryCount { get; set; }
        public int UnassignedDishCount { get; set; }
    }

    public class ProductionLineSetupModel
    {
        public Guid Id { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public decimal? CapacityMealPerBatch { get; set; }
        public decimal? MinutesPerBatch { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
        public int DishCount { get; set; }
        public List<DishCategoryLineModel> ListCategory { get; set; } = [];
    }

    public class DishCategoryLineModel
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public Guid? ProductionLineId { get; set; }
        public string? ProductionLineName { get; set; }
        /// <summary>Đang gán dây chuyền đã ngừng → coi như chưa gán.</summary>
        public bool LineInactive { get; set; }
        public int DishCount { get; set; }
    }

    /// <summary>UI-6.26: form dây chuyền lưu kèm danh sách nhóm món (một giao dịch). Mã trống → máy chủ tự sinh DC-NNN.</summary>
    public class ProductionLineSaveRequest
    {
        public Guid? Id { get; set; }
        [MaxLength(50)]
        public string? Code { get; set; }
        [MaxLength(500)]
        public string? Name { get; set; }
        public decimal? CapacityMealPerBatch { get; set; }
        public decimal? MinutesPerBatch { get; set; }
        [MaxLength(50)]
        public string? Status { get; set; }
        [MaxLength(2000)]
        public string? Note { get; set; }
        public List<Guid> ListDishCategoryId { get; set; } = [];
    }

    /// <summary>UI-6.26: Gán nhanh – danh sách {nhóm món, dây chuyền} (dây chuyền trống / Guid rỗng = bỏ gán), một giao dịch.</summary>
    public class DishCategoryAssignLineRequest
    {
        public List<DishCategoryAssignLineItem> ListItem { get; set; } = [];
    }

    public class DishCategoryAssignLineItem
    {
        public Guid DishCategoryId { get; set; }
        public Guid? ProductionLineId { get; set; }
    }
}
