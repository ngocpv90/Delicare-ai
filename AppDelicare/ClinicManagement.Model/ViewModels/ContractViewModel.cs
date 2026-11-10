using System;

namespace AppDelicare.Model.ViewModels
{
	public class ContractViewModel : BaseViewModel
	{
		public Guid CustomerId { get; set; }
		public string? CustomerName { get; set; }
		public string? ContractNumber { get; set; }
		public string? Name { get; set; }
		public DateTime? SignedDate { get; set; }
		public DateTime EffectiveFrom { get; set; }
		public DateTime? EffectiveTo { get; set; }
		public decimal? ContractValue { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
