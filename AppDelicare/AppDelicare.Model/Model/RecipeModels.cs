using AppDelicare.Model.Entity;

namespace AppDelicare.Model.Model
{
    /// <summary>T45R (spec 5.2): một phiên bản công thức kèm dòng định lượng và giá vốn NVL / suất.</summary>
    public class RecipeVersionModel
    {
        public Recipe? Recipe { get; set; }
        public List<RecipeLine> ListLine { get; set; } = [];
        public decimal CostPerMeal { get; set; }
        /// <summary>false = có dòng thiếu giá NCC / thiếu quy đổi đơn vị.</summary>
        public bool CostComplete { get; set; }
    }

    /// <summary>T45R (spec 5.2 bước 6): giá vốn NVL của công thức.</summary>
    public class RecipeCostModel
    {
        public Guid RecipeId { get; set; }
        public decimal MealCount { get; set; }
        /// <summary>Số suất chuẩn của định lượng (Recipe.StandardOutputQty, mặc định 100).</summary>
        public decimal StandardServings { get; set; }
        public List<RecipeLineCostModel> ListLine { get; set; } = [];
        public decimal TotalMaterialCost { get; set; }
        public decimal CostPerMeal { get; set; }
        public bool Complete { get; set; }
    }

    public class RecipeLineCostModel
    {
        public Guid LineId { get; set; }
        public Guid IngredientId { get; set; }
        public string? IngredientName { get; set; }
        public decimal Quantity { get; set; }
        public Guid UnitId { get; set; }
        public decimal? UnitPrice { get; set; }
        public Guid? PriceUnitId { get; set; }
        public decimal? Amount { get; set; }
        public string? Warning { get; set; }
    }
}
