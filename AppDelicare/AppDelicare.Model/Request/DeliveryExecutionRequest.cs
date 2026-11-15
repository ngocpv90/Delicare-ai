using System;

namespace AppDelicare.Model.Request
{
	public class DeliveryExecutionRequest : BaseRequest
	{
		public Guid DeliveryOrderId { get; set; }
		public DateTime? DepartedAt { get; set; }
		public DateTime? ArrivedAt { get; set; }
		public DateTime? DeliveredAt { get; set; }
		public string? DriverName { get; set; }
		public string? VehicleCode { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
