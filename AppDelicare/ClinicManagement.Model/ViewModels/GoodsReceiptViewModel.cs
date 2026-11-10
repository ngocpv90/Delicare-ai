using System;

namespace AppDelicare.Model.ViewModels
{
	public class GoodsReceiptViewModel : BaseViewModel
	{
		public Guid? PurchaseOrderId { get; set; }
		public string? PurchaseOrderName { get; set; }
		public Guid SupplierId { get; set; }
		public string? SupplierName { get; set; }
		public string? ReceiptNumber { get; set; }
		public DateTime ReceiptDate { get; set; }
		public Guid? WarehouseId { get; set; }
		public string? WarehouseName { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
