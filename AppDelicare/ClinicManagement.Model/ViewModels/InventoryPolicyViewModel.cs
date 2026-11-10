using System;

namespace AppDelicare.Model.ViewModels
{
	public class InventoryPolicyViewModel : BaseViewModel
	{
		public Guid IngredientId { get; set; }
		public string? IngredientName { get; set; }
		public Guid? WarehouseId { get; set; }
		public string? WarehouseName { get; set; }
		public decimal? MinimumStock { get; set; }
		public decimal? MaximumStock { get; set; }
		public decimal? SafetyStock { get; set; }
		public bool UseFefo { get; set; }
		public int? ExpiryWarningDays { get; set; }
		public string? Status { get; set; }
	}
}
