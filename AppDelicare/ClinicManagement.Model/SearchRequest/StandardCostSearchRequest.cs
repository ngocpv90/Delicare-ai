using System;

namespace AppDelicare.Model.SearchRequest
{
	public class StandardCostSearchRequest : BaseSearchRequest
	{
		public Guid DishId { get; set; }
		public Guid? RecipeId { get; set; }
		public DateTime? EffectiveFrom { get; set; }
		public DateTime? EffectiveTo { get; set; }
		public string? CurrencyCode { get; set; }
		public string? Status { get; set; }
	}
}
