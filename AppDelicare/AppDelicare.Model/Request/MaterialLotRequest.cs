using System.ComponentModel.DataAnnotations;
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
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public decimal? UnitCost { get; set; }
		[MaxLength(50, ErrorMessage = "QcStatus tối đa 50 ký tự.")]
		public string? QcStatus { get; set; }
		public string? Note { get; set; }
	}
}
