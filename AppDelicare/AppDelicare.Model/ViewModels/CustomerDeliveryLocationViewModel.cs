using System;

namespace AppDelicare.Model.ViewModels
{
	public class CustomerDeliveryLocationViewModel : BaseViewModel
	{
		public Guid CustomerId { get; set; }
		public string? CustomerName { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? Address { get; set; }
		public string? ContactName { get; set; }
		public string? ContactPhone { get; set; }
		public string? DeliveryInstructions { get; set; }
		public string? Status { get; set; }
	}
}
