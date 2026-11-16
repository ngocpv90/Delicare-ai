using System;

namespace AppDelicare.Model.SearchRequest
{
	public class ShiftSearchRequest : BaseSearchRequest
	{
		public Guid? CompanyId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public TimeSpan? StartTime { get; set; }
		public TimeSpan? EndTime { get; set; }
		public string? Status { get; set; }
	}
}
