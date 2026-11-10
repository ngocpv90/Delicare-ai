using System;

namespace AppDelicare.Model.ViewModels
{
	public class PurchaseOrderLineViewModel : BaseViewModel
	{
		public Guid PurchaseOrderId { get; set; }
		public string? PurchaseOrderName { get; set; }
		public Guid IngredientId { get; set; }
		public string? IngredientName { get; set; }
		public decimal Quantity { get; set; }
		public Guid UnitId { get; set; }
		public string? UnitName { get; set; }
		public decimal UnitPrice { get; set; }
		public decimal LineAmount { get; set; }
		public string? Note { get; set; }
	}
}
