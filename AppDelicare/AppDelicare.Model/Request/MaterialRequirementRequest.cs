using System;

namespace AppDelicare.Model.Request
{
	public class MaterialRequirementRequest : BaseRequest
	{
		public Guid? ProductionPlanId { get; set; }
		public Guid IngredientId { get; set; }
		public decimal RequiredQuantity { get; set; }
		public decimal AvailableQuantity { get; set; }
		public decimal ExpectedIncomingQuantity { get; set; }
		public decimal PurchaseQuantity { get; set; }
		public Guid UnitId { get; set; }
		public string? Status { get; set; }
	}
}
