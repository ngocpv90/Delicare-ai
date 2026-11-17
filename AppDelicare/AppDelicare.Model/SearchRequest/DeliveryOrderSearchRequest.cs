using System;

namespace AppDelicare.Model.SearchRequest
{
	public class DeliveryOrderSearchRequest : BaseSearchRequest
	{
		public Guid? OrderId { get; set; }
		public Guid? CustomerDeliveryLocationId { get; set; }
		public Guid? DeliveryRouteId { get; set; }
		public string? DeliveryNumber { get; set; }
		public string? Status { get; set; }
	}
}
