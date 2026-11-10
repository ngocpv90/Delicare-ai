using System;

namespace AppDelicare.Model.SearchRequest
{
	public class ProofOfDeliverySearchRequest : BaseSearchRequest
	{
		public Guid DeliveryOrderId { get; set; }
		public DateTime ConfirmedAt { get; set; }
	}
}
