using System;

namespace AppDelicare.Model.SearchRequest
{
	public class RecipeLineSearchRequest : BaseSearchRequest
	{
		public Guid? RecipeId { get; set; }
		public Guid? IngredientId { get; set; }
		public Guid? UnitId { get; set; }
	}
}
