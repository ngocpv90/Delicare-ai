using System;

namespace AppDelicare.Model.ViewModels
{
	public class StockCountLineViewModel : BaseViewModel
	{
		public Guid StockCountId { get; set; }
		public string? StockCountName { get; set; }
		public Guid IngredientId { get; set; }
		public string? IngredientName { get; set; }
		public Guid? MaterialLotId { get; set; }
		public string? MaterialLotName { get; set; }
		public decimal SystemQuantity { get; set; }
		public decimal PhysicalQuantity { get; set; }
		public decimal VarianceQuantity { get; set; }
		public Guid UnitId { get; set; }
		public string? UnitName { get; set; }
		public string? VarianceReason { get; set; }
	}
}
