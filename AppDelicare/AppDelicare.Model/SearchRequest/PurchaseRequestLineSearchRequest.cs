using System;

namespace AppDelicare.Model.SearchRequest
{
	public class PurchaseRequestLineSearchRequest : BaseSearchRequest
	{
		public Guid? PurchaseRequestId { get; set; }
		public Guid? IngredientId { get; set; }
		public Guid? UnitId { get; set; }
		public DateTime? RequiredDate { get; set; }
	}
}
