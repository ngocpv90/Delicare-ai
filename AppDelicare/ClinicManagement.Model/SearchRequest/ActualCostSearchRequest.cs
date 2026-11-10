using System;

namespace AppDelicare.Model.SearchRequest
{
	public class ActualCostSearchRequest : BaseSearchRequest
	{
		public Guid? ProductionBatchId { get; set; }
		public Guid? ProductionOrderId { get; set; }
		public decimal TotalCost { get; set; }
		public string? CurrencyCode { get; set; }
		public DateTime CalculatedAt { get; set; }
	}
}
