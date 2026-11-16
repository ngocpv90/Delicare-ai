using System;

namespace AppDelicare.Model.SearchRequest
{
	public class DishSearchRequest : BaseSearchRequest
	{
		public Guid? DishCategoryId { get; set; }
		public Guid? DefaultUnitId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? Status { get; set; }
	}
}
