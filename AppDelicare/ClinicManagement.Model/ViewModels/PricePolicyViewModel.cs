using System;

namespace AppDelicare.Model.ViewModels
{
	public class PricePolicyViewModel : BaseViewModel
	{
		public Guid? CustomerId { get; set; }
		public string? CustomerName { get; set; }
		public Guid? ContractId { get; set; }
		public string? ContractName { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public decimal UnitPrice { get; set; }
		public string? CurrencyCode { get; set; }
		public DateTime EffectiveFrom { get; set; }
		public DateTime? EffectiveTo { get; set; }
		public string? Status { get; set; }
	}
}
