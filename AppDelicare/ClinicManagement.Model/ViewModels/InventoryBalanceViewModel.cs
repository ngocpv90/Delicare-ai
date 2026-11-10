using System;

namespace AppDelicare.Model.ViewModels
{
	public class InventoryBalanceViewModel : BaseViewModel
	{
		public Guid WarehouseId { get; set; }
		public string? WarehouseName { get; set; }
		public Guid IngredientId { get; set; }
		public string? IngredientName { get; set; }
		public Guid? MaterialLotId { get; set; }
		public string? MaterialLotName { get; set; }
		public Guid? StorageLocationId { get; set; }
		public string? StorageLocationName { get; set; }
		public decimal OnHandQuantity { get; set; }
		public decimal ReservedQuantity { get; set; }
		public decimal AvailableQuantity { get; set; }
		public Guid UnitId { get; set; }
		public string? UnitName { get; set; }
		public DateTime LastCalculatedDate { get; set; }
	}
}
