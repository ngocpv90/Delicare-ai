using System;

namespace AppDelicare.Model.ViewModels
{
	public class DishViewModel : BaseViewModel
	{
		public Guid? DishCategoryId { get; set; }
		public string? DishCategoryName { get; set; }
		public Guid? DefaultUnitId { get; set; }
		public string? DefaultUnitName { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? Description { get; set; }
		public decimal? StandardServingSize { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
