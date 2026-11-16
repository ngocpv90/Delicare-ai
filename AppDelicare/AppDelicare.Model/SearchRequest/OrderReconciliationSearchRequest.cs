using System;

namespace AppDelicare.Model.SearchRequest
{
	public class OrderReconciliationSearchRequest : BaseSearchRequest
	{
		public Guid OrderId { get; set; }
		public Guid? ProofOfDeliveryId { get; set; }
		public DateTime? ReconciliationDate { get; set; }
		public string? Status { get; set; }
	}
}
