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
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public string? CIF { get; set; }
		public string? DisplayName { get; set; }
		public string? PhoneNumber { get; set; }
		public string? FirstName { get; set; }
		public string? LastName { get; set; }
		public string? Gender { get; set; }
		public DateTime? DateOfBirth { get; set; }
		public string? IdentityNumber { get; set; }
		public DateTime? IdentityDate { get; set; }
		public string? IdentityPlace { get; set; }
		public string? Occupation { get; set; }
		public string? LegalName { get; set; }
		public string? BusinessRegistrationNumber { get; set; }
		public DateTime? RegistrationDate { get; set; }
		public string? RegistrationPlace { get; set; }
		public string? CompanySize { get; set; }
		public string? Industry { get; set; }
		public string? LegalRepresentativeName { get; set; }
		public string? ContactPersonName { get; set; }
		public string? ContactPersonPhone { get; set; }
		public string? ContactPersonEmail { get; set; }
		public string? Segment { get; set; }
		public string? Source { get; set; }
		public Guid? RegionId { get; set; }
		public Guid? AreaId { get; set; }
		public Guid? BranchId { get; set; }
		public string? BranchName { get; set; }
		public Guid? DepartmentId { get; set; }
		public string? DepartmentName { get; set; }
		public Guid? UserAssignId { get; set; }
		public string? ExternalData { get; set; }
		public string? ExternalValue { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
