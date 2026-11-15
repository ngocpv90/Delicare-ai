using System;

namespace AppDelicare.Model.SearchRequest
{
	public class MenuLineSearchRequest : BaseSearchRequest
	{
		public Guid MenuId { get; set; }
		public Guid DishId { get; set; }
		public string? MealType { get; set; }
	}
}
