using System;

namespace AppDelicare.Model.ViewModels
{
	public class CustomerViewModel : BaseViewModel
	{
		public Guid? CompanyId { get; set; }
		public string? CompanyName { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? TaxCode { get; set; }
		public string? Phone { get; set; }
		public string? Email { get; set; }
		public string? Address { get; set; }
		public string? CustomerType { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
