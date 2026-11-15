using System;

namespace AppDelicare.Model.ViewModels
{
	public class DeliveryRouteViewModel : BaseViewModel
	{
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? Description { get; set; }
		public string? Status { get; set; }
	}
}
