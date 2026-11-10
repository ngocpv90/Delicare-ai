using System;

namespace AppDelicare.Model.Request
{
	public class MaterialConsumptionVarianceRequest : BaseRequest
	{
		public Guid ProductionOrderId { get; set; }
		public Guid IngredientId { get; set; }
		public decimal StandardQuantity { get; set; }
		public decimal ActualQuantity { get; set; }
		public decimal VarianceQuantity { get; set; }
		public decimal? VarianceCost { get; set; }
		public string? Reason { get; set; }
	}
}
