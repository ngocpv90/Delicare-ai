using System;

namespace AppDelicare.Model.ViewModels
{
	public class MenuAssignmentViewModel : BaseViewModel
	{
		public Guid CustomerId { get; set; }
		public string? CustomerName { get; set; }
		public Guid? ContractId { get; set; }
		public string? ContractName { get; set; }
		public Guid MenuId { get; set; }
		public string? MenuName { get; set; }
		public Guid? DeliveryLocationId { get; set; }
		public string? DeliveryLocationName { get; set; }
		public DateTime EffectiveFrom { get; set; }
		public DateTime? EffectiveTo { get; set; }
		public string? Status { get; set; }
	}
}
