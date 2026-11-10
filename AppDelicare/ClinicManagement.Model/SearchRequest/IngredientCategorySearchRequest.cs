using System;

namespace AppDelicare.Model.SearchRequest
{
	public class IngredientCategorySearchRequest : BaseSearchRequest
	{
		public Guid? ParentId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? Status { get; set; }
	}
}
