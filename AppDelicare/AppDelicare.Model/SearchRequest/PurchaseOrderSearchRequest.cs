using System;

namespace AppDelicare.Model.SearchRequest
{
	public class PurchaseOrderSearchRequest : BaseSearchRequest
	{
		public Guid? SupplierId { get; set; }
		public Guid? PurchaseRequestId { get; set; }
		public string? PurchaseOrderNumber { get; set; }
		public DateTime? OrderDate { get; set; }
		public DateTime? ExpectedDeliveryDate { get; set; }
		public string? Status { get; set; }
		public decimal? TotalAmount { get; set; }
	}
}
