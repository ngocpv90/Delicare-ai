using System;

namespace AppDelicare.Model.SearchRequest
{
	public class MenuSearchRequest : BaseSearchRequest
	{
		public Guid? CompanyId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? MenuType { get; set; }
		public string? VersionCode { get; set; }
		public DateTime EffectiveFrom { get; set; }
		public DateTime? EffectiveTo { get; set; }
		public string? Status { get; set; }
	}
}
