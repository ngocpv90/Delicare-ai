using System;

namespace AppDelicare.Model.ViewModels
{
	public class DeliveryLoadingViewModel : BaseViewModel
	{
		public Guid DeliveryOrderId { get; set; }
		public string? DeliveryOrderName { get; set; }
		public Guid? EmployeeId { get; set; }
		public string? EmployeeName { get; set; }
		public string? VehicleCode { get; set; }
		public DateTime LoadingDate { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
