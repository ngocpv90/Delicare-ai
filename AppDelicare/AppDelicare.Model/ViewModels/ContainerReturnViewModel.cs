using System;

namespace AppDelicare.Model.ViewModels
{
	public class ContainerReturnViewModel : BaseViewModel
	{
		public Guid? DeliveryOrderId { get; set; }
		public string? DeliveryOrderName { get; set; }
		public Guid? CustomerId { get; set; }
		public string? CustomerName { get; set; }
		public DateTime ReturnDate { get; set; }
		public int ExpectedQuantity { get; set; }
		public int ReturnedQuantity { get; set; }
		public int DamagedQuantity { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
