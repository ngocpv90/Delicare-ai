using System;

namespace AppDelicare.Model.Request
{
	public class OrganizationUnitRequest : BaseRequest
	{
		public Guid? CompanyId { get; set; }
		public Guid? ParentId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? UnitType { get; set; }
		public string? Address { get; set; }
		public string? Phone { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
