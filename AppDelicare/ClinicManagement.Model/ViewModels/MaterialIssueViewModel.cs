using System;

namespace AppDelicare.Model.ViewModels
{
	public class MaterialIssueViewModel : BaseViewModel
	{
		public Guid ProductionOrderId { get; set; }
		public string? ProductionOrderName { get; set; }
		public Guid WarehouseId { get; set; }
		public string? WarehouseName { get; set; }
		public string? IssueNumber { get; set; }
		public DateTime IssueDate { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
