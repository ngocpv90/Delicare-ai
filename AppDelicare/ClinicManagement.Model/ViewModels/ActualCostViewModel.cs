using System;

namespace AppDelicare.Model.ViewModels
{
	public class ActualCostViewModel : BaseViewModel
	{
		public Guid? ProductionBatchId { get; set; }
		public string? ProductionBatchName { get; set; }
		public Guid? ProductionOrderId { get; set; }
		public string? ProductionOrderName { get; set; }
		public decimal MaterialCost { get; set; }
		public decimal LaborCost { get; set; }
		public decimal OverheadCost { get; set; }
		public decimal OtherCost { get; set; }
		public decimal TotalCost { get; set; }
		public string? CurrencyCode { get; set; }
		public DateTime CalculatedAt { get; set; }
	}
}
