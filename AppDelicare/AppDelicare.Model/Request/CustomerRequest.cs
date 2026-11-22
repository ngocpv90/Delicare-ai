using System.ComponentModel.DataAnnotations;
using System;

namespace AppDelicare.Model.Request
{
	public class CustomerRequest : BaseRequest
	{
		public Guid? CompanyId { get; set; }
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
		[MaxLength(50, ErrorMessage = "CIF tối đa 50 ký tự.")]
		public string? CIF { get; set; }
		[MaxLength(255, ErrorMessage = "DisplayName tối đa 255 ký tự.")]
		public string? DisplayName { get; set; }
		[MaxLength(50, ErrorMessage = "PhoneNumber tối đa 50 ký tự.")]
		public string? PhoneNumber { get; set; }
		[MaxLength(100, ErrorMessage = "FirstName tối đa 100 ký tự.")]
		public string? FirstName { get; set; }
		[MaxLength(100, ErrorMessage = "LastName tối đa 100 ký tự.")]
		public string? LastName { get; set; }
		[MaxLength(20, ErrorMessage = "Gender tối đa 20 ký tự.")]
		public string? Gender { get; set; }
		public DateTime? DateOfBirth { get; set; }
		[MaxLength(50, ErrorMessage = "IdentityNumber tối đa 50 ký tự.")]
		public string? IdentityNumber { get; set; }
		public DateTime? IdentityDate { get; set; }
		[MaxLength(255, ErrorMessage = "IdentityPlace tối đa 255 ký tự.")]
		public string? IdentityPlace { get; set; }
		[MaxLength(100, ErrorMessage = "Occupation tối đa 100 ký tự.")]
		public string? Occupation { get; set; }
		[MaxLength(255, ErrorMessage = "LegalName tối đa 255 ký tự.")]
		public string? LegalName { get; set; }
		[MaxLength(50, ErrorMessage = "BusinessRegistrationNumber tối đa 50 ký tự.")]
		public string? BusinessRegistrationNumber { get; set; }
		public DateTime? RegistrationDate { get; set; }
		[MaxLength(255, ErrorMessage = "RegistrationPlace tối đa 255 ký tự.")]
		public string? RegistrationPlace { get; set; }
		[MaxLength(50, ErrorMessage = "CompanySize tối đa 50 ký tự.")]
		public string? CompanySize { get; set; }
		[MaxLength(100, ErrorMessage = "Industry tối đa 100 ký tự.")]
		public string? Industry { get; set; }
		[MaxLength(255, ErrorMessage = "LegalRepresentativeName tối đa 255 ký tự.")]
		public string? LegalRepresentativeName { get; set; }
		[MaxLength(255, ErrorMessage = "ContactPersonName tối đa 255 ký tự.")]
		public string? ContactPersonName { get; set; }
		[MaxLength(50, ErrorMessage = "ContactPersonPhone tối đa 50 ký tự.")]
		public string? ContactPersonPhone { get; set; }
		[MaxLength(255, ErrorMessage = "ContactPersonEmail tối đa 255 ký tự.")]
		public string? ContactPersonEmail { get; set; }
		[MaxLength(50, ErrorMessage = "Segment tối đa 50 ký tự.")]
		public string? Segment { get; set; }
		[MaxLength(100, ErrorMessage = "Source tối đa 100 ký tự.")]
		public string? Source { get; set; }
		public Guid? RegionId { get; set; }
		public Guid? AreaId { get; set; }
		public Guid? BranchId { get; set; }
		public Guid? DepartmentId { get; set; }
		public Guid? UserAssignId { get; set; }
		public string? ExternalData { get; set; }
		[MaxLength(500, ErrorMessage = "ExternalValue tối đa 500 ký tự.")]
		public string? ExternalValue { get; set; }
		/// <summary>UI-5.8 (D-63 r): chỉ khi thêm mới – điểm giao đầu tiên tạo cùng transaction (khóa form <c>FirstLocation.Name</c>…); bỏ qua khi sửa.</summary>
		public CustomerFirstLocationRequest? FirstLocation { get; set; }
	}

	/// <summary>UI-5.8: điểm giao đầu tiên của khách mới – tên, địa chỉ, khung giờ nhận (từ &lt; đến).</summary>
	public class CustomerFirstLocationRequest
	{
		[MaxLength(255, ErrorMessage = "Tên điểm giao tối đa 255 ký tự.")]
		public string? Name { get; set; }
		public string? Address { get; set; }
		public TimeSpan? DeliveryWindowFrom { get; set; }
		public TimeSpan? DeliveryWindowTo { get; set; }

		public bool IsEmpty => string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Address) && DeliveryWindowFrom == null && DeliveryWindowTo == null;
	}
}
