using System;

namespace AppDelicare.Model.ViewModels
{
	public class MaterialConsumptionVarianceViewModel : BaseViewModel
	{
		public Guid ProductionOrderId { get; set; }
		public string? ProductionOrderName { get; set; }
		public Guid IngredientId { get; set; }
		public string? IngredientName { get; set; }
		public decimal StandardQuantity { get; set; }
		public decimal ActualQuantity { get; set; }
		public decimal VarianceQuantity { get; set; }
		public decimal? VarianceCost { get; set; }
		public string? Reason { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public decimal? TheoreticalQty { get; set; }
		public decimal? VariancePercent { get; set; }
		public decimal? VarianceValue { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
