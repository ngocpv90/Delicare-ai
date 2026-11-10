using System;

namespace AppDelicare.Model.Request
{
	public class DeliveryLoadingRequest : BaseRequest
	{
		public Guid DeliveryOrderId { get; set; }
		public Guid? EmployeeId { get; set; }
		public string? VehicleCode { get; set; }
		public DateTime LoadingDate { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
