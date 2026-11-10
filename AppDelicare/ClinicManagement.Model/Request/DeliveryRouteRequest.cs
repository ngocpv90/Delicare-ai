using System;

namespace AppDelicare.Model.Request
{
	public class DeliveryRouteRequest : BaseRequest
	{
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? Description { get; set; }
		public string? Status { get; set; }
	}
}
