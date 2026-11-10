using System;

namespace AppDelicare.Model.Request
{
	public class RecipeLineRequest : BaseRequest
	{
		public Guid RecipeId { get; set; }
		public Guid IngredientId { get; set; }
		public decimal Quantity { get; set; }
		public Guid UnitId { get; set; }
		public decimal? ExpectedWasteRate { get; set; }
		public string? PreparationNote { get; set; }
		public int SortOrder { get; set; }
	}
}
