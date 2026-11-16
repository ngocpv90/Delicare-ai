using System;

namespace AppDelicare.Model.ViewModels
{
	public class OrganizationUnitViewModel : BaseViewModel
	{
		public Guid? CompanyId { get; set; }
		public string? CompanyName { get; set; }
		public Guid? ParentId { get; set; }
		public string? ParentName { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? UnitType { get; set; }
		public string? Address { get; set; }
		public string? Phone { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public Guid? ManagerId { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
