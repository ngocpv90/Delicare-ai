using AppDelicare.Model.Entity;

namespace AppDelicare.Model.Model
{
    /// <summary>T45R (spec 4.1): hồ sơ khách hàng 360°.</summary>
    public class CustomerOverviewModel
    {
        public Customer? Customer { get; set; }
        public List<CustomerContact> ListContact { get; set; } = [];
        public List<CustomerDeliveryLocation> ListLocation { get; set; } = [];
        public List<ContractOverviewModel> ListContract { get; set; } = [];
        /// <summary>15 đơn gần nhất theo ngày suất ăn.</summary>
        public List<OrderBriefModel> ListRecentOrder { get; set; } = [];
        public int OrderCount { get; set; }
        /// <summary>Doanh thu đã đối soát (tổng OrderReconciliation.FinalAmount).</summary>
        public decimal ReconciledRevenue { get; set; }
        public int ComplaintCount { get; set; }
        /// <summary>Số suất trung bình / ngày trong 7 ngày gần nhất (đơn Delivered / Reconciled).</summary>
        public decimal AvgMealsPerDay7d { get; set; }
    }

    public class ContractOverviewModel
    {
        public Guid Id { get; set; }
        public string? ContractNumber { get; set; }
        public string? Name { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Status { get; set; }
        public decimal? ContractValue { get; set; }
        public List<ContractMeal> ListMeal { get; set; } = [];
    }

    public class OrderBriefModel
    {
        public Guid Id { get; set; }
        public string? OrderNumber { get; set; }
        public DateTime? MealDate { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public decimal? TotalQuantity { get; set; }
        public decimal? TotalAmount { get; set; }
        public string? Status { get; set; }
    }

    /// <summary>T45R (spec 4.3): tổng quan nhà cung cấp.</summary>
    public class SupplierOverviewModel
    {
        public Supplier? Supplier { get; set; }
        public List<SupplierMaterialBriefModel> ListMaterial { get; set; } = [];
        public List<PurchaseOrder> ListOpenPurchaseOrder { get; set; } = [];
        /// <summary>20 sự cố gần nhất.</summary>
        public List<SupplierIssue> ListIssue { get; set; } = [];
        /// <summary>Tỷ lệ giao đúng hạn 90 ngày (%), null nếu chưa có phiếu nhập gắn PO có ngày dự kiến.</summary>
        public decimal? OnTimeRate { get; set; }
        /// <summary>Tỷ lệ trả hàng 90 ngày (% số lượng), null nếu chưa nhập.</summary>
        public decimal? RejectRate { get; set; }
    }

    public class SupplierMaterialBriefModel
    {
        public Guid Id { get; set; }
        public Guid IngredientId { get; set; }
        public string? IngredientName { get; set; }
        public decimal? UnitPrice { get; set; }
        public bool IsPreferred { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public string? Status { get; set; }
    }

    /// <summary>T45R (spec 4.6): sơ đồ kho – khu bảo quản → vị trí.</summary>
    public class WarehouseLayoutModel
    {
        public Warehouse? Warehouse { get; set; }
        public List<StorageZoneLayoutModel> ListZone { get; set; } = [];
    }

    public class StorageZoneLayoutModel
    {
        public StorageZone? Zone { get; set; }
        public List<StorageLocation> ListLocation { get; set; } = [];
    }
}
