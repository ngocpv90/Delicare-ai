using System;

namespace AppDelicare.Model.SearchRequest
{
	public class ProductionOutputSearchRequest : BaseSearchRequest
	{
		public Guid ProductionBatchId { get; set; }
		public Guid DishId { get; set; }
		public Guid? UnitId { get; set; }
		public string? QualityStatus { get; set; }
	}
}
