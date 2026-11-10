using System;

namespace AppDelicare.Model.ViewModels
{
	public class EmployeeViewModel : BaseViewModel
	{
		public Guid? CompanyId { get; set; }
		public string? CompanyName { get; set; }
		public Guid? OrganizationUnitId { get; set; }
		public string? OrganizationUnitName { get; set; }
		public Guid? PositionId { get; set; }
		public string? PositionName { get; set; }
		public string? Code { get; set; }
		public string? FullName { get; set; }
		public DateTime? DateOfBirth { get; set; }
		public string? Gender { get; set; }
		public string? Phone { get; set; }
		public string? Email { get; set; }
		public string? Address { get; set; }
		public DateTime? HireDate { get; set; }
		public string? Status { get; set; }
	}
}
