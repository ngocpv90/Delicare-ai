using System;

namespace AppDelicare.Model.SearchRequest
{
	public class InventoryTransactionSearchRequest : BaseSearchRequest
	{
		public Guid? WarehouseId { get; set; }
		public Guid? IngredientId { get; set; }
		public Guid? MaterialLotId { get; set; }
		public Guid? StorageLocationId { get; set; }
		public string? TransactionNumber { get; set; }
		public string? TransactionType { get; set; }
		public DateTime? TransactionDate { get; set; }
		public Guid? UnitId { get; set; }
		public string? ReferenceType { get; set; }
		public Guid? ReferenceId { get; set; }
	}
}
