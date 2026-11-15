using System;

namespace AppDelicare.Model.ViewModels
{
	public class RecipeViewModel : BaseViewModel
	{
		public Guid DishId { get; set; }
		public string? DishName { get; set; }
		public string? VersionCode { get; set; }
		public decimal YieldQuantity { get; set; }
		public Guid? YieldUnitId { get; set; }
		public string? YieldUnitName { get; set; }
		public DateTime EffectiveFrom { get; set; }
		public DateTime? EffectiveTo { get; set; }
		public bool IsActive { get; set; }
		public string? Instructions { get; set; }
		public string? Status { get; set; }
	}
}
