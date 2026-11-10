using System;

namespace AppDelicare.Model.Request
{
	public class PurchaseOrderRequest : BaseRequest
	{
		public Guid SupplierId { get; set; }
		public Guid? PurchaseRequestId { get; set; }
		public string? PurchaseOrderNumber { get; set; }
		public DateTime OrderDate { get; set; }
		public DateTime? ExpectedDeliveryDate { get; set; }
		public string? Status { get; set; }
		public decimal? TotalAmount { get; set; }
		public string? Note { get; set; }
	}
}
