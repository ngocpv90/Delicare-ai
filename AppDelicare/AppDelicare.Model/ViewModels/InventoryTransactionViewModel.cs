using System;

namespace AppDelicare.Model.ViewModels
{
	public class InventoryTransactionViewModel : BaseViewModel
	{
		public Guid WarehouseId { get; set; }
		public string? WarehouseName { get; set; }
		public Guid IngredientId { get; set; }
		public string? IngredientName { get; set; }
		public Guid? MaterialLotId { get; set; }
		public string? MaterialLotName { get; set; }
		public Guid? StorageLocationId { get; set; }
		public string? StorageLocationName { get; set; }
		public string? TransactionNumber { get; set; }
		public string? TransactionType { get; set; }
		public DateTime TransactionDate { get; set; }
		public decimal QuantityIn { get; set; }
		public decimal QuantityOut { get; set; }
		public Guid UnitId { get; set; }
		public string? UnitName { get; set; }
		public string? ReferenceType { get; set; }
		public Guid? ReferenceId { get; set; }
		public string? ReferenceName { get; set; }
		public string? Reason { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public decimal? Quantity { get; set; }
		public decimal? UnitCost { get; set; }
		public string? Note { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
