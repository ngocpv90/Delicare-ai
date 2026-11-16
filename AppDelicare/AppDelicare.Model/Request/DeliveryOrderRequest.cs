using System;

namespace AppDelicare.Model.Request
{
	public class DeliveryOrderRequest : BaseRequest
	{
		public Guid OrderId { get; set; }
		public Guid? CustomerDeliveryLocationId { get; set; }
		public Guid? DeliveryRouteId { get; set; }
		public string? DeliveryNumber { get; set; }
		public DateTime PlannedDeliveryTime { get; set; }
		public DateTime? ActualDeliveryTime { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public Guid? CustomerId { get; set; }
		public DateTime? DeliveryDate { get; set; }
	}
}
