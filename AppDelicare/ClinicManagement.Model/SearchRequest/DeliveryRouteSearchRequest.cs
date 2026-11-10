using System;

namespace AppDelicare.Model.SearchRequest
{
	public class DeliveryRouteSearchRequest : BaseSearchRequest
	{
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? Status { get; set; }
	}
}
