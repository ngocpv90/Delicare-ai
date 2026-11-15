using System;

namespace AppDelicare.Model.SearchRequest
{
	public class SupplierSearchRequest : BaseSearchRequest
	{
		public Guid? CompanyId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? TaxCode { get; set; }
		public string? Phone { get; set; }
		public string? Email { get; set; }
		public string? Status { get; set; }
	}
}
