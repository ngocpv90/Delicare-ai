using System;

namespace AppDelicare.Model.Request
{
	public class MaterialLotRequest : BaseRequest
	{
		public Guid IngredientId { get; set; }
		public Guid? SupplierId { get; set; }
		public Guid? GoodsReceiptLineId { get; set; }
		public string? LotNumber { get; set; }
		public DateTime? ManufactureDate { get; set; }
		public DateTime? ExpiryDate { get; set; }
		public DateTime? ReceivedDate { get; set; }
		public string? QualityStatus { get; set; }
		public string? Status { get; set; }
	}
}
