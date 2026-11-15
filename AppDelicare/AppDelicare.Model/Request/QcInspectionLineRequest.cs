using System;

namespace AppDelicare.Model.Request
{
	public class QcInspectionLineRequest : BaseRequest
	{
		public Guid QcInspectionId { get; set; }
		public Guid? GoodsReceiptLineId { get; set; }
		public string? Criterion { get; set; }
		public string? Result { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public Guid? QCInspectionId { get; set; }
		public Guid? IngredientId { get; set; }
		public decimal? Temperature { get; set; }
		public decimal? Weight { get; set; }
		public string? Appearance { get; set; }
		public string? Packaging { get; set; }
		public DateTime? ExpiryDate { get; set; }
		public string? Remark { get; set; }
	}
}
