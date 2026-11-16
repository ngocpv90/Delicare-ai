using System;

namespace AppDelicare.Model.Request
{
	public class MaterialIssueRequest : BaseRequest
	{
		public Guid ProductionOrderId { get; set; }
		public Guid WarehouseId { get; set; }
		public string? IssueNumber { get; set; }
		public DateTime IssueDate { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
