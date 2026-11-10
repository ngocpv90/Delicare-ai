using System;

namespace AppDelicare.Model.ViewModels
{
	public class MenuLineViewModel : BaseViewModel
	{
		public Guid MenuId { get; set; }
		public string? MenuName { get; set; }
		public Guid DishId { get; set; }
		public string? DishName { get; set; }
		public string? MealType { get; set; }
		public int? DayOfWeek { get; set; }
		public int SortOrder { get; set; }
		public decimal? StandardQuantity { get; set; }
		public string? Note { get; set; }
	}
}
