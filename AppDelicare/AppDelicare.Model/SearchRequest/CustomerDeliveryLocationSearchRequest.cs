using System;

namespace AppDelicare.Model.SearchRequest
{
	public class CustomerDeliveryLocationSearchRequest : BaseSearchRequest
	{
		public Guid CustomerId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? Status { get; set; }
	}
}
