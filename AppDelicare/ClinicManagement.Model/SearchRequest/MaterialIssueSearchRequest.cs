using System;

namespace AppDelicare.Model.SearchRequest
{
	public class MaterialIssueSearchRequest : BaseSearchRequest
	{
		public Guid ProductionOrderId { get; set; }
		public Guid WarehouseId { get; set; }
		public DateTime IssueDate { get; set; }
		public string? Status { get; set; }
	}
}
