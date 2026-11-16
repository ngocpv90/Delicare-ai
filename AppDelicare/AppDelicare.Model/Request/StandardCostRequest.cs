using System;

namespace AppDelicare.Model.Request
{
	public class StandardCostRequest : BaseRequest
	{
		public Guid DishId { get; set; }
		public Guid? RecipeId { get; set; }
		public decimal CostPerServing { get; set; }
		public DateTime EffectiveFrom { get; set; }
		public DateTime? EffectiveTo { get; set; }
		public string? CurrencyCode { get; set; }
		public string? Status { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public decimal? MaterialCost { get; set; }
		public decimal? LaborCost { get; set; }
		public decimal? OverheadCost { get; set; }
		public decimal? TotalCost { get; set; }
	}
}
