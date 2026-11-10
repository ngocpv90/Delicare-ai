using System;

namespace AppDelicare.Model.SearchRequest
{
	public class PurchaseOrderLineSearchRequest : BaseSearchRequest
	{
		public Guid PurchaseOrderId { get; set; }
		public Guid IngredientId { get; set; }
		public Guid UnitId { get; set; }
	}
}
