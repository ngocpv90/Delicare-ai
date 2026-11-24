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
		/// <summary>HR-ACC-1.6 (D-82 f): ô "Cấp tài khoản web" trên form nhân viên – lưu xong tạo yêu cầu duyệt "Cấp tài khoản" cho FIN-IT.</summary>
		public bool RequestWebAccount { get; set; }
		/// <summary>HR-ACC-1.6: nhóm quyền đề xuất (tên nhóm, cách nhau bằng dấu phẩy) – trống = nhóm gợi ý theo chức danh (BusinessRules:PositionDefaultGroups).</summary>
		public string? AccountGroups { get; set; }
	}
}
