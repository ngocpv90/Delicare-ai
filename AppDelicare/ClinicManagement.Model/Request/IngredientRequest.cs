using System;

namespace AppDelicare.Model.Request
{
	public class IngredientRequest : BaseRequest
	{
		public Guid? CompanyId { get; set; }
		public Guid? IngredientCategoryId { get; set; }
		public Guid? BaseUnitId { get; set; }
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
