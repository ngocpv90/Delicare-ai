using System;

namespace AppDelicare.Model.SearchRequest
{
	public class RecipeSearchRequest : BaseSearchRequest
	{
		public Guid? DishId { get; set; }
		public string? VersionCode { get; set; }
		public Guid? YieldUnitId { get; set; }
		public DateTime? EffectiveFrom { get; set; }
		public DateTime? EffectiveTo { get; set; }
		public string? Status { get; set; }
	}
}
