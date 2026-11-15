using System;

namespace AppDelicare.Model.SearchRequest
{
	public class GoodsReceiptSearchRequest : BaseSearchRequest
	{
		public Guid? PurchaseOrderId { get; set; }
		public Guid SupplierId { get; set; }
		public string? ReceiptNumber { get; set; }
		public DateTime? ReceiptDate { get; set; }
		public Guid? WarehouseId { get; set; }
		public string? Status { get; set; }
	}
}
