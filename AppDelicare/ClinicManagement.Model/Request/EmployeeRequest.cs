using System;

namespace AppDelicare.Model.Request
{
	public class EmployeeRequest : BaseRequest
	{
		public Guid? CompanyId { get; set; }
		public Guid? OrganizationUnitId { get; set; }
		public Guid? PositionId { get; set; }
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
