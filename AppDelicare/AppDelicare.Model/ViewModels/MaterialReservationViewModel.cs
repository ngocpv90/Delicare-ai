using System;

namespace AppDelicare.Model.ViewModels
{
	public class MaterialReservationViewModel : BaseViewModel
	{
		public Guid ProductionOrderId { get; set; }
		public string? ProductionOrderName { get; set; }
		public Guid IngredientId { get; set; }
		public string? IngredientName { get; set; }
		public decimal ReservedQuantity { get; set; }
		public Guid UnitId { get; set; }
		public string? UnitName { get; set; }
		public string? Status { get; set; }
	}
}
