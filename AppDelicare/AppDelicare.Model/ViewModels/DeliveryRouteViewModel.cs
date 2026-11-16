using System;

namespace AppDelicare.Model.ViewModels
{
	public class DeliveryRouteViewModel : BaseViewModel
	{
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? Description { get; set; }
		public string? Status { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public string? RouteNo { get; set; }
		public DateTime? DeliveryDate { get; set; }
		public Guid? VehicleId { get; set; }
		public string? VehicleName { get; set; }
		public Guid? DriverId { get; set; }
		public DateTime? PlannedStart { get; set; }
		public DateTime? PlannedEnd { get; set; }
		public DateTime? ActualStart { get; set; }
		public DateTime? ActualEnd { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
