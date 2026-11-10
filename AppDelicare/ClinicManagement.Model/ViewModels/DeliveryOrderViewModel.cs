using System;

namespace AppDelicare.Model.ViewModels
{
	public class DeliveryOrderViewModel : BaseViewModel
	{
		public Guid OrderId { get; set; }
		public string? OrderName { get; set; }
		public Guid? CustomerDeliveryLocationId { get; set; }
		public string? CustomerDeliveryLocationName { get; set; }
		public Guid? DeliveryRouteId { get; set; }
		public string? DeliveryRouteName { get; set; }
		public string? DeliveryNumber { get; set; }
		public DateTime PlannedDeliveryTime { get; set; }
		public DateTime? ActualDeliveryTime { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
