using System;

namespace AppDelicare.Model.Request
{
	public class MenuLineRequest : BaseRequest
	{
		public Guid MenuId { get; set; }
		public Guid DishId { get; set; }
		public string? MealType { get; set; }
		public int? DayOfWeek { get; set; }
		public int SortOrder { get; set; }
		public decimal? StandardQuantity { get; set; }
		public string? Note { get; set; }
	}
}
