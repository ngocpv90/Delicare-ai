using System;

namespace AppDelicare.Model.ViewModels
{
	public class WarehouseViewModel : BaseViewModel
	{
		public Guid? CompanyId { get; set; }
		public string? CompanyName { get; set; }
		public Guid? OrganizationUnitId { get; set; }
		public string? OrganizationUnitName { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? WarehouseType { get; set; }
		public string? Address { get; set; }
		public string? Status { get; set; }
	}
}
