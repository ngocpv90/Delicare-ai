using System;

namespace AppDelicare.Model.SearchRequest
{
	public class ProductionBatchSearchRequest : BaseSearchRequest
	{
		public Guid? ProductionOrderId { get; set; }
		public string? BatchNumber { get; set; }
		public DateTime? StartDate { get; set; }
		public DateTime? EndDate { get; set; }
		public string? QualityStatus { get; set; }
		public string? Status { get; set; }
	}
}
