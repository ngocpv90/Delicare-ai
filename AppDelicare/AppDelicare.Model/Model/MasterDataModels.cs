namespace AppDelicare.Model.Model
{
    /// <summary>T45R (spec 4.4): tồn khả dụng của 1 nguyên liệu.</summary>
    public class IngredientStockModel
    {
        public Guid IngredientId { get; set; }
        public decimal AvailableQuantity { get; set; }
        public decimal? MinimumStock { get; set; }
        public bool BelowMinimum { get; set; }
        public List<IngredientStockByWarehouse> ListWarehouse { get; set; } = [];
    }

    public class IngredientStockByWarehouse
    {
        public Guid WarehouseId { get; set; }
        public decimal AvailableQuantity { get; set; }
    }

    /// <summary>T45R (spec 5.4): đơn giá áp dụng và nguồn (PricePolicy:Contract / PricePolicy:Customer / ContractMeal).</summary>
    public class PriceResolveModel
    {
        public decimal UnitPrice { get; set; }
        public string? Source { get; set; }
        public Guid SourceId { get; set; }
    }
}
