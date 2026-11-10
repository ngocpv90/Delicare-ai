using System;

namespace AppDelicare.Model.ViewModels
{
	public class IngredientViewModel : BaseViewModel
	{
		public Guid? CompanyId { get; set; }
		public string? CompanyName { get; set; }
		public Guid? IngredientCategoryId { get; set; }
		public string? IngredientCategoryName { get; set; }
		public Guid? BaseUnitId { get; set; }
		public string? BaseUnitName { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? Specification { get; set; }
		public string? StorageCondition { get; set; }
		public bool IsExpiryManaged { get; set; }
		public int? ShelfLifeDays { get; set; }
		public decimal? MinimumStock { get; set; }
		public decimal? SafetyStock { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
	}
}
