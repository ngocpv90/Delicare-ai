using System;

namespace AppDelicare.Model.SearchRequest
{
	public class PositionSearchRequest : BaseSearchRequest
	{
		public Guid? CompanyId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? Status { get; set; }
	}
}
