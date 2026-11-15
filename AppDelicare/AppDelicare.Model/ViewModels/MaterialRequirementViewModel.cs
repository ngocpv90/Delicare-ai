using System;

namespace AppDelicare.Model.ViewModels
{
	public class MaterialRequirementViewModel : BaseViewModel
	{
		public Guid? ProductionPlanId { get; set; }
		public string? ProductionPlanName { get; set; }
		public Guid IngredientId { get; set; }
		public string? IngredientName { get; set; }
		public decimal RequiredQuantity { get; set; }
		public decimal AvailableQuantity { get; set; }
		public decimal ExpectedIncomingQuantity { get; set; }
		public decimal PurchaseQuantity { get; set; }
		public Guid UnitId { get; set; }
		public string? UnitName { get; set; }
		public string? Status { get; set; }
	}
}
