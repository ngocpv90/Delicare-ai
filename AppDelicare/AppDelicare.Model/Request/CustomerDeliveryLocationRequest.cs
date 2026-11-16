using System;

namespace AppDelicare.Model.Request
{
	public class CustomerDeliveryLocationRequest : BaseRequest
	{
		public Guid CustomerId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? Address { get; set; }
		public string? ContactName { get; set; }
		public string? ContactPhone { get; set; }
		public string? DeliveryInstructions { get; set; }
		public string? Status { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public decimal? Latitude { get; set; }
		public decimal? Longitude { get; set; }
		public TimeSpan? DeliveryWindowFrom { get; set; }
		public TimeSpan? DeliveryWindowTo { get; set; }
	}
}
