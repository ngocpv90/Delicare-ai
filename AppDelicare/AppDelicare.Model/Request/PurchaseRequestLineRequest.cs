using System;

namespace AppDelicare.Model.Request
{
	public class PurchaseRequestLineRequest : BaseRequest
	{
		public Guid PurchaseRequestId { get; set; }
		public Guid IngredientId { get; set; }
		public decimal Quantity { get; set; }
		public Guid UnitId { get; set; }
		public DateTime? RequiredDate { get; set; }
		public string? Note { get; set; }
	}
}
