using System;

namespace AppDelicare.Model.ViewModels
{
	public class QcInspectionLineViewModel : BaseViewModel
	{
		public Guid QcInspectionId { get; set; }
		public string? QcInspectionName { get; set; }
		public Guid? GoodsReceiptLineId { get; set; }
		public string? GoodsReceiptLineName { get; set; }
		public string? Criterion { get; set; }
		public string? Result { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public Guid? IngredientId { get; set; }
		public string? IngredientName { get; set; }
		public decimal? Temperature { get; set; }
		public decimal? Weight { get; set; }
		public string? Appearance { get; set; }
		public string? Packaging { get; set; }
		public DateTime? ExpiryDate { get; set; }
		public string? Remark { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
