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
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public Guid? DeliveryRouteId { get; set; }
		public decimal? LoadedQty { get; set; }
		public DateTime? LoadingTime { get; set; }
	}
}
