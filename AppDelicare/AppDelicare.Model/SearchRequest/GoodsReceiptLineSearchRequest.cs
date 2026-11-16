using System;

namespace AppDelicare.Model.SearchRequest
{
	public class GoodsReceiptLineSearchRequest : BaseSearchRequest
	{
		public Guid? GoodsReceiptId { get; set; }
		public Guid? IngredientId { get; set; }
		public Guid? UnitId { get; set; }
		public string? LotNumber { get; set; }
		public DateTime? ManufactureDate { get; set; }
		public DateTime? ExpiryDate { get; set; }
		public string? QualityStatus { get; set; }
	}
}
