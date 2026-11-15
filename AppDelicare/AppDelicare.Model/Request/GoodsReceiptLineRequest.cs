using System;

namespace AppDelicare.Model.Request
{
	public class GoodsReceiptLineRequest : BaseRequest
	{
		public Guid GoodsReceiptId { get; set; }
		public Guid IngredientId { get; set; }
		public decimal ReceivedQuantity { get; set; }
		public decimal AcceptedQuantity { get; set; }
		public decimal RejectedQuantity { get; set; }
		public Guid UnitId { get; set; }
		public string? LotNumber { get; set; }
		public DateTime? ManufactureDate { get; set; }
		public DateTime? ExpiryDate { get; set; }
		public string? QualityStatus { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public decimal? Quantity { get; set; }
		public decimal? UnitCost { get; set; }
		public Guid? LotId { get; set; }
	}
}
