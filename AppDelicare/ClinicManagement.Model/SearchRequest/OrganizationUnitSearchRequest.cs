using System;

namespace AppDelicare.Model.SearchRequest
{
	public class OrganizationUnitSearchRequest : BaseSearchRequest
	{
		public Guid? CompanyId { get; set; }
		public Guid? ParentId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? UnitType { get; set; }
		public string? Phone { get; set; }
		public string? Status { get; set; }
	}
}
