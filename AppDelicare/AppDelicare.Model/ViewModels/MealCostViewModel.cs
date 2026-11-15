using System;

namespace AppDelicare.Model.ViewModels
{
	public class MealCostViewModel : BaseViewModel
	{
		public Guid? OrderId { get; set; }
		public string? OrderName { get; set; }
		public Guid? OrderLineId { get; set; }
		public string? OrderLineName { get; set; }
		public Guid? DishId { get; set; }
		public string? DishName { get; set; }
		public decimal AcceptedMealQuantity { get; set; }
		public decimal TotalActualCost { get; set; }
		public decimal CostPerMeal { get; set; }
		public decimal? Revenue { get; set; }
		public decimal? GrossMargin { get; set; }
		public DateTime CalculatedAt { get; set; }
	}
}
