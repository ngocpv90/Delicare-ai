using System;

namespace AppDelicare.Model.Request
{
	public class InventoryBalanceRequest : BaseRequest
	{
		public Guid WarehouseId { get; set; }
		public Guid IngredientId { get; set; }
		public Guid? MaterialLotId { get; set; }
		public Guid? StorageLocationId { get; set; }
		public decimal OnHandQuantity { get; set; }
		public decimal ReservedQuantity { get; set; }
		public decimal AvailableQuantity { get; set; }
		public Guid UnitId { get; set; }
		public DateTime LastCalculatedDate { get; set; }
	}
}
