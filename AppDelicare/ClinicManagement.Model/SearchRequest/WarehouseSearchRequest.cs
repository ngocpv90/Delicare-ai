using System;

namespace AppDelicare.Model.SearchRequest
{
	public class WarehouseSearchRequest : BaseSearchRequest
	{
		public Guid? CompanyId { get; set; }
		public Guid? OrganizationUnitId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? WarehouseType { get; set; }
		public string? Status { get; set; }
	}
}
