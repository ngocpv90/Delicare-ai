using System;

namespace AppDelicare.Model.SearchRequest
{
	public class InventoryBalanceSearchRequest : BaseSearchRequest
	{
		public Guid WarehouseId { get; set; }
		public Guid IngredientId { get; set; }
		public Guid? MaterialLotId { get; set; }
		public Guid? StorageLocationId { get; set; }
		public Guid UnitId { get; set; }
		public DateTime LastCalculatedDate { get; set; }
	}
}
