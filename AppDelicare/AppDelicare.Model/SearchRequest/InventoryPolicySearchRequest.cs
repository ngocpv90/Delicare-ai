using System;

namespace AppDelicare.Model.SearchRequest
{
	public class InventoryPolicySearchRequest : BaseSearchRequest
	{
		public Guid IngredientId { get; set; }
		public Guid? WarehouseId { get; set; }
		public int? ExpiryWarningDays { get; set; }
		public string? Status { get; set; }
	}
}
