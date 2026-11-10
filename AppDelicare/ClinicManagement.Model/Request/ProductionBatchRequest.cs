using System;

namespace AppDelicare.Model.Request
{
	public class ProductionBatchRequest : BaseRequest
	{
		public Guid ProductionOrderId { get; set; }
		public string? BatchNumber { get; set; }
		public DateTime StartDate { get; set; }
		public DateTime? EndDate { get; set; }
		public decimal? PlannedQuantity { get; set; }
		public decimal? ActualQuantity { get; set; }
		public string? QualityStatus { get; set; }
		public string? Status { get; set; }
	}
}
