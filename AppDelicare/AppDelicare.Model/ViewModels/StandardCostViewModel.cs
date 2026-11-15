using System;

namespace AppDelicare.Model.ViewModels
{
	public class StandardCostViewModel : BaseViewModel
	{
		public Guid DishId { get; set; }
		public string? DishName { get; set; }
		public Guid? RecipeId { get; set; }
		public string? RecipeName { get; set; }
		public decimal CostPerServing { get; set; }
		public DateTime EffectiveFrom { get; set; }
		public DateTime? EffectiveTo { get; set; }
		public string? CurrencyCode { get; set; }
		public string? Status { get; set; }
	}
}
