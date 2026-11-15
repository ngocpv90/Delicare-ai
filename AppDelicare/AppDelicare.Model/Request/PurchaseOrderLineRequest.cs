using System;

namespace AppDelicare.Model.Request
{
	public class PurchaseOrderLineRequest : BaseRequest
	{
		public Guid PurchaseOrderId { get; set; }
		public Guid IngredientId { get; set; }
		public decimal Quantity { get; set; }
		public Guid UnitId { get; set; }
		public decimal UnitPrice { get; set; }
		public decimal LineAmount { get; set; }
		public string? Note { get; set; }
	}
}
