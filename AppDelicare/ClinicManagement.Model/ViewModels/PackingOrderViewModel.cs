using System;

namespace AppDelicare.Model.ViewModels
{
	public class PackingOrderViewModel : BaseViewModel
	{
		public Guid? ProductionBatchId { get; set; }
		public string? ProductionBatchName { get; set; }
		public Guid? OrderId { get; set; }
		public string? OrderName { get; set; }
		public string? PackingOrderNumber { get; set; }
		public DateTime PackingDate { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
