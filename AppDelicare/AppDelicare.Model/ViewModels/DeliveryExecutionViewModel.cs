using System;

namespace AppDelicare.Model.ViewModels
{
	public class DeliveryExecutionViewModel : BaseViewModel
	{
		public Guid DeliveryOrderId { get; set; }
		public string? DeliveryOrderName { get; set; }
		public DateTime? DepartedAt { get; set; }
		public DateTime? ArrivedAt { get; set; }
		public DateTime? DeliveredAt { get; set; }
		public string? DriverName { get; set; }
		public string? VehicleCode { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
