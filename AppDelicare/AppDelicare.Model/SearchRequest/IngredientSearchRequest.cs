using System;

namespace AppDelicare.Model.SearchRequest
{
	public class IngredientSearchRequest : BaseSearchRequest
	{
		public Guid? CompanyId { get; set; }
		public Guid? IngredientCategoryId { get; set; }
		public Guid? BaseUnitId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public bool IsExpiryManaged { get; set; }
		public string? Status { get; set; }
	}
}
